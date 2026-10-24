using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// X-Plane 12 风格 3D 历史飞行轨迹渲染器 (Flight Path Recorder)
/// 专为《SimplePlanes 2》/ Unity 6 URP 定制的高内聚 Procedural Mesh 轨迹核心组件。
/// 
/// 核心特性：
/// 1. 机翼姿态飘带 (Wing Ribbon)：双面 Quad Strip 实体网格，忠实反映飞机 Roll/Pitch 翻滚俯仰。
/// 2. 地面高度幕布 (Vertical Curtain)：飞机轨迹至地表/海平面半透明投影，带周期进近下滑道深色刻度线。
/// 3. 智能采样与滑动窗口：基于 (距离 > 3m 或 姿态角度 > 5°) 触发，环形缓冲区定容管理 (5000点)，32位索引无GC。
/// 4. 浮动原点 (Floating Origin) 错位双重防护：
///    - 原生订阅 SimplePlanes 2 的 FloatingOriginScript.Instance.Repositioned 事件；
///    - 帧间位移阈值自适应后备检测 (防止跨大世界坐标系瞬移拉扯)。
/// 5. 飞行状态顶点着色：支持按瞬时地速 (m/s) 或 垂直升降率 (Vertical Speed) 动态多段渐变着色。
/// 6. 快捷键交互：F8 循环切换显示模式，F9 一键清空轨迹。
/// </summary>
public class FlightPathRecorder : MonoBehaviour
{
    // ==========================================
    // 公开枚举与配置选项
    // ==========================================
    public enum DisplayMode
    {
        Off = 0,               // 关闭渲染
        RibbonOnly = 1,        // 仅机翼姿态飘带
        RibbonAndCurtain = 2,  // 机翼姿态飘带 + 地面高度幕布 (X-Plane 12 标准样式)
        SingleLine = 3         // 仅中心单线轨迹 (超窄宽度)
    }

    public enum ColorGradientMode
    {
        GroundSpeed,    // 根据标量地速 (m/s) 顶点渐变着色
        VerticalSpeed   // 根据垂直升降速度 (m/s) 顶点渐变着色
    }

    // ==========================================
    // 轨迹采样点数据结构
    // ==========================================
    public struct PathPoint
    {
        public Vector3 CenterPos;      // 飞机几何中心 (世界坐标)
        public Vector3 LeftWingPos;    // 左翼尖端位置 (世界坐标)
        public Vector3 RightWingPos;   // 右翼尖端位置 (世界坐标)
        public Vector3 GroundPos;      // 地形或海平面投射交点 (世界坐标)
        public float Speed;            // 瞬时标量速度 (m/s)
        public float VerticalSpeed;    // 垂直升降率 (m/s)
        public bool IsMilestoneTick;   // 是否为进近下滑道加深网格刻度线
    }

    // ==========================================
    // 基础参数配置
    // ==========================================
    [Header("---- 采样与性能设置 ----")]
    [Tooltip("轨迹点最大容量 (超出自动丢弃最早历史点)")]
    public int maxCapacity = 5000;

    [Tooltip("位移采样阈值 (米): 超过此距离记录新点")]
    public float minDistanceThreshold = 3.0f;

    [Tooltip("姿态采样阈值 (度): 飞机旋转角度变化超过此值记录新点")]
    public float minAngleThreshold = 5.0f;

    [Tooltip("机翼飘带宽度/翼展 (米)")]
    public float ribbonWingspan = 12.0f;

    [Tooltip("地面幕布加深垂直刻度线的周期间隔采样点数")]
    public int milestoneInterval = 10;

    [Tooltip("地面检测射线最大探测距离 (若未触地则投射至海平面 Y=0)")]
    public float groundRaycastMaxDistance = 5000.0f;

    [Tooltip("地面检测物理图层")]
    public LayerMask groundLayerMask = ~0;

    [Header("---- 浮动原点 (Floating Origin) 检测 ----")]
    [Tooltip("触发自适应浮动原点平移的单帧位移距离阈值 (米)")]
    public float floatingOriginShiftThreshold = 500.0f;

    [Header("---- 顶点颜色渐变配置 ----")]
    public ColorGradientMode colorMode = ColorGradientMode.GroundSpeed;

    [Tooltip("低速色彩 (如 < 50 m/s / ~97 kts)")]
    public Color colorLowSpeed = new Color(0.12f, 0.52f, 1.0f, 0.88f);     // 冰蓝

    [Tooltip("巡航色彩 (如 130 m/s / ~250 kts)")]
    public Color colorNormalSpeed = new Color(0.12f, 0.90f, 0.28f, 0.88f);  // 翠绿

    [Tooltip("高速色彩 (如 240 m/s / ~466 kts)")]
    public Color colorHighSpeed = new Color(1.0f, 0.82f, 0.12f, 0.88f);    // 亮黄

    [Tooltip("极速色彩 (如 > 340 m/s 超音速)")]
    public Color colorMaxSpeed = new Color(1.0f, 0.18f, 0.12f, 0.88f);     // 艳红

    [Header("---- 交互状态 ----")]
    public DisplayMode currentMode = DisplayMode.RibbonAndCurtain;

    // ==========================================
    // 内部状态与缓冲区
    // ==========================================
    private readonly List<PathPoint> _pointBuffer = new List<PathPoint>(5120);

    // 飞机遥测缓存
    private Transform _targetTransform;
    private Rigidbody _targetRigidbody;
    private Vector3 _currentAircraftPos;
    private Quaternion _currentAircraftRot;
    private Vector3 _currentAircraftVelocity;
    private bool _hasAircraftData = false;

    // 历史采样基准
    private Vector3 _lastSamplePos;
    private Quaternion _lastSampleRot;
    private bool _hasFirstSample = false;

    // 浮动原点自适应追踪
    private Vector3 _prevFrameAircraftPos;
    private bool _hasPrevFramePos = false;
    private bool _nativeFloatingOriginSubscribed = false;

    // Mesh 渲染子物体与组件
    private GameObject _ribbonContainer;
    private GameObject _curtainContainer;
    private MeshFilter _ribbonFilter;
    private MeshRenderer _ribbonRenderer;
    private MeshFilter _curtainFilter;
    private MeshRenderer _curtainRenderer;

    private Mesh _ribbonMesh;
    private Mesh _curtainMesh;
    private Material _proceduralMaterial;

    // 零 GC 预分配几何构建容器
    private readonly List<Vector3> _meshVertices = new List<Vector3>(10240);
    private readonly List<int> _meshTriangles = new List<int>(30720);
    private readonly List<Color> _meshColors = new List<Color>(10240);

    // ==========================================
    // 生命周期
    // ==========================================
    private void Awake()
    {
        InitializeRenderers();
    }

    private void Start()
    {
        TrySubscribeNativeFloatingOrigin();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
    }

    private void LateUpdate()
    {
        // 1. 获取解耦后的飞机空间与速度数据
        UpdateAircraftData();

        if (!_hasAircraftData || _targetTransform == null)
            return;

        // 2. 检查并修正浮动原点跳变 (后备自动检测)
        CheckAndHandleFloatingOriginShift();

        // 3. 距离与姿态角度自适应采样
        EvaluateAndSamplePoint();

        // 4. 重建三维网格几何
        RebuildProceduralMeshes();

        // 记录上一帧坐标供下帧浮动原点位移差分
        _prevFrameAircraftPos = _currentAircraftPos;
        _hasPrevFramePos = true;
    }

    private void OnDestroy()
    {
        UnsubscribeNativeFloatingOrigin();

        if (_ribbonContainer != null) Destroy(_ribbonContainer);
        if (_curtainContainer != null) Destroy(_curtainContainer);
        if (_ribbonMesh != null) Destroy(_ribbonMesh);
        if (_curtainMesh != null) Destroy(_curtainMesh);
        if (_proceduralMaterial != null) Destroy(_proceduralMaterial);
    }

    // =========================================================================
    // 1. 飞机遥测数据解耦更新 (SimplePlanes 2 官方 API 注入点)
    // =========================================================================
    /// <summary>
    /// 解耦获取当前受控飞机的坐标、旋转和瞬时速度。
    /// 无论在何种 Unity 环境下均可平滑运行，优先挂接 SimplePlanes 2 原生 API。
    /// </summary>
    public void UpdateAircraftData()
    {
        // =====================================================================
        // 【SimplePlanes 2 原生 API 替换指南】：
        // 
        // 在 SimplePlanes 2 (Game.dll) 生产环境下，可直接通过以下官方单例读取：
        //
        // try 
        // {
        //     var playerAircraft = Assets.Scripts.Flight.FlightSceneScript.Instance?.LocalPlayer?.Aircraft;
        //     if (playerAircraft != null) 
        //     {
        //         _targetTransform = playerAircraft.Transform;
        //         _currentAircraftPos = playerAircraft.Transform.position;
        //         _currentAircraftRot = playerAircraft.Transform.rotation;
        //         _currentAircraftVelocity = (playerAircraft.Rigidbody != null) 
        //             ? playerAircraft.Rigidbody.velocity 
        //             : (playerAircraft.Transform.forward * playerAircraft.AirSpeed);
        //         _hasAircraftData = true;
        //         return;
        //     }
        // }
        // catch { /* 容错守卫 */ }
        // =====================================================================

        // 通用 Fallback：若无原生 API 注入，自动探测当前 GameObject、刚体或主相机父级
        if (_targetTransform == null)
        {
            _targetTransform = this.transform;
            _targetRigidbody = GetComponent<Rigidbody>();

            if (_targetRigidbody == null && Camera.main != null && Camera.main.transform.parent != null)
            {
                _targetTransform = Camera.main.transform.parent;
                _targetRigidbody = _targetTransform.GetComponent<Rigidbody>();
            }
        }

        if (_targetTransform != null)
        {
            _currentAircraftPos = _targetTransform.position;
            _currentAircraftRot = _targetTransform.rotation;

            if (_targetRigidbody != null)
            {
                _currentAircraftVelocity = _targetRigidbody.velocity;
            }
            else
            {
                // 若无物理刚体，通过前后帧位置差分平滑估算真实速度
                if (_hasPrevFramePos && Time.deltaTime > 0.0001f)
                {
                    _currentAircraftVelocity = (_currentAircraftPos - _prevFrameAircraftPos) / Time.deltaTime;
                }
                else
                {
                    _currentAircraftVelocity = _targetTransform.forward * 60f;
                }
            }

            _hasAircraftData = true;
        }
        else
        {
            _hasAircraftData = false;
        }
    }

    // =========================================================================
    // 2. 智能空间采样与定容环形缓冲管理
    // =========================================================================
    private void EvaluateAndSamplePoint()
    {
        if (!_hasFirstSample)
        {
            RecordNewSamplePoint();
            return;
        }

        // 空间欧式距离平方差分
        float distSqr = (_currentAircraftPos - _lastSamplePos).sqrMagnitude;
        // 四元数角位移差分
        float angleDelta = Quaternion.Angle(_currentAircraftRot, _lastSampleRot);

        if (distSqr >= (minDistanceThreshold * minDistanceThreshold) || angleDelta >= minAngleThreshold)
        {
            RecordNewSamplePoint();
        }
    }

    private void RecordNewSamplePoint()
    {
        PathPoint pt = new PathPoint();
        pt.CenterPos = _currentAircraftPos;

        // 计算当前机翼左右两端世界坐标 (考虑飞机的 Roll/Pitch 姿态)
        Vector3 wingRightVector = (_currentAircraftRot * Vector3.right).normalized;
        float halfSpan = (currentMode == DisplayMode.SingleLine) ? 0.08f : (ribbonWingspan * 0.5f);

        pt.LeftWingPos = _currentAircraftPos - (wingRightVector * halfSpan);
        pt.RightWingPos = _currentAircraftPos + (wingRightVector * halfSpan);

        // 地面高度射线投射 (向下探测地形网格)
        Ray ray = new Ray(_currentAircraftPos + Vector3.up * 1.5f, Vector3.down);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, groundRaycastMaxDistance, groundLayerMask))
        {
            pt.GroundPos = hit.point + Vector3.up * 0.04f; // 抬升 4cm 消除与地表共面 Z-Fighting
        }
        else
        {
            // 若在海面上空或未击中碰撞体，优雅投射至海平面 Y=0
            pt.GroundPos = new Vector3(_currentAircraftPos.x, 0.0f, _currentAircraftPos.z);
        }

        pt.Speed = _currentAircraftVelocity.magnitude;
        pt.VerticalSpeed = _currentAircraftVelocity.y;
        pt.IsMilestoneTick = (_pointBuffer.Count % milestoneInterval == 0);

        // 环形缓冲容量控制：达到上限时抛弃最早点
        if (_pointBuffer.Count >= maxCapacity)
        {
            _pointBuffer.RemoveAt(0);
        }

        _pointBuffer.Add(pt);

        _lastSamplePos = _currentAircraftPos;
        _lastSampleRot = _currentAircraftRot;
        _hasFirstSample = true;
    }

    // =========================================================================
    // 3. 浮动原点 (Floating Origin) 错位补偿系统
    // =========================================================================
    /// <summary>
    /// 开放外部接口：当游戏原生浮动原点系统重置世界坐标时直接调用。
    /// </summary>
    /// <param name="offset">世界坐标变化量</param>
    public void OnFloatingOriginShift(Vector3 offset)
    {
        ApplyOffsetToAllHistory(offset);
    }

    /// <summary>
    /// 后备自适应跳变检测：若未捕获到原生事件，自动检测单帧超常位移并平移历史轨迹
    /// </summary>
    private void CheckAndHandleFloatingOriginShift()
    {
        if (!_hasPrevFramePos) return;

        Vector3 frameDelta = _currentAircraftPos - _prevFrameAircraftPos;
        // 如果单帧位移大于阈值 (如 500m，正常 20ms 帧内不可能移动 500m = 25000 m/s = 73马赫)
        if (frameDelta.sqrMagnitude > (floatingOriginShiftThreshold * floatingOriginShiftThreshold))
        {
            // 剔除飞机正常的物理速度位移
            Vector3 physicalDelta = _currentAircraftVelocity * Time.deltaTime;
            Vector3 worldShiftOffset = frameDelta - physicalDelta;

            // 逆向平移历史点坐标，消除拉扯错位
            ApplyOffsetToAllHistory(-worldShiftOffset);

            _lastSamplePos -= worldShiftOffset;
            _prevFrameAircraftPos = _currentAircraftPos;
        }
    }

    private void ApplyOffsetToAllHistory(Vector3 offset)
    {
        for (int i = 0; i < _pointBuffer.Count; i++)
        {
            PathPoint pt = _pointBuffer[i];
            pt.CenterPos += offset;
            pt.LeftWingPos += offset;
            pt.RightWingPos += offset;
            pt.GroundPos += offset;
            _pointBuffer[i] = pt;
        }
    }

    /// <summary>
    /// 尝试通过反射自动挂接 SimplePlanes 2 原生 FloatingOriginScript 事件
    /// </summary>
    private void TrySubscribeNativeFloatingOrigin()
    {
        if (_nativeFloatingOriginSubscribed) return;

        try
        {
            Type originType = Type.GetType("Assets.Scripts.Flight.FloatingOriginScript, Game");
            if (originType != null)
            {
                var instanceProp = originType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object instance = instanceProp != null ? instanceProp.GetValue(null, null) : null;
                if (instance != null)
                {
                    var eventInfo = originType.GetEvent("Repositioned");
                    if (eventInfo != null)
                    {
                        var handlerMethod = this.GetType().GetMethod("OnNativeOriginRepositioned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Delegate handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, handlerMethod);
                        eventInfo.AddEventHandler(instance, handler);
                        _nativeFloatingOriginSubscribed = true;
                    }
                }
            }
        }
        catch { /* 游戏未集成或类型不存在时不干扰正常运行 */ }
    }

    private void UnsubscribeNativeFloatingOrigin()
    {
        if (!_nativeFloatingOriginSubscribed) return;
        try
        {
            Type originType = Type.GetType("Assets.Scripts.Flight.FloatingOriginScript, Game");
            if (originType != null)
            {
                var instanceProp = originType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object instance = instanceProp != null ? instanceProp.GetValue(null, null) : null;
                if (instance != null)
                {
                    var eventInfo = originType.GetEvent("Repositioned");
                    if (eventInfo != null)
                    {
                        var handlerMethod = this.GetType().GetMethod("OnNativeOriginRepositioned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Delegate handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, handlerMethod);
                        eventInfo.RemoveEventHandler(instance, handler);
                        _nativeFloatingOriginSubscribed = false;
                    }
                }
            }
        }
        catch { }
    }

    // 原生事件签名反射回调
    private void OnNativeOriginRepositioned(object sender, EventArgs args)
    {
        try
        {
            var deltaProp = args.GetType().GetProperty("Delta");
            if (deltaProp != null)
            {
                Vector3 delta = (Vector3)deltaProp.GetValue(args, null);
                // SimplePlanes 2 原生 Repositioned 触发时，世界所有物体移动了 -Delta
                OnFloatingOriginShift(-delta);
            }
        }
        catch { }
    }

    // =========================================================================
    // 4. Procedural Mesh 动态网格构建 (双面渲染 + 顶点色)
    // =========================================================================
    private void RebuildProceduralMeshes()
    {
        if (currentMode == DisplayMode.Off || _pointBuffer.Count < 2)
        {
            if (_ribbonMesh != null) _ribbonMesh.Clear();
            if (_curtainMesh != null) _curtainMesh.Clear();
            return;
        }

        // 1. 构建机翼姿态实体飘带
        BuildWingRibbonMesh();

        // 2. 构建垂直高度投影幕布
        if (currentMode == DisplayMode.RibbonAndCurtain)
        {
            BuildVerticalCurtainMesh();
        }
        else
        {
            if (_curtainMesh != null) _curtainMesh.Clear();
        }
    }

    /// <summary>
    /// 构建反映横滚与俯仰的姿态飘带 (双面 Quad Strip)
    /// </summary>
    private void BuildWingRibbonMesh()
    {
        _meshVertices.Clear();
        _meshTriangles.Clear();
        _meshColors.Clear();

        int count = _pointBuffer.Count;

        // 填充左右翼尖端顶点与色彩
        for (int i = 0; i < count; i++)
        {
            PathPoint pt = _pointBuffer[i];
            _meshVertices.Add(pt.LeftWingPos);   // 顶点索引: 2 * i
            _meshVertices.Add(pt.RightWingPos);  // 顶点索引: 2 * i + 1

            Color ptColor = EvaluatePointColor(pt);
            _meshColors.Add(ptColor);
            _meshColors.Add(ptColor);
        }

        // 构建双面三角形索引 (正面 + 反面，消灭背面剔除隐形)
        for (int i = 0; i < count - 1; i++)
        {
            int l0 = 2 * i;
            int r0 = 2 * i + 1;
            int l1 = 2 * (i + 1);
            int r1 = 2 * (i + 1) + 1;

            // ---- 正面三角形 (Normal Upwards) ----
            _meshTriangles.Add(l0);
            _meshTriangles.Add(l1);
            _meshTriangles.Add(r0);

            _meshTriangles.Add(r0);
            _meshTriangles.Add(l1);
            _meshTriangles.Add(r1);

            // ---- 反面三角形 (Normal Downwards) ----
            _meshTriangles.Add(l0);
            _meshTriangles.Add(r0);
            _meshTriangles.Add(l1);

            _meshTriangles.Add(r0);
            _meshTriangles.Add(r1);
            _meshTriangles.Add(l1);
        }

        _ribbonMesh.Clear();
        _ribbonMesh.indexFormat = IndexFormat.UInt32; // 开启 32 位索引支持大顶点量
        _ribbonMesh.SetVertices(_meshVertices);
        _ribbonMesh.SetColors(_meshColors);
        _ribbonMesh.SetTriangles(_meshTriangles, 0);
        _ribbonMesh.RecalculateBounds();
    }

    /// <summary>
    /// 构建 X-Plane 12 风格半透明垂直下滑道幕布
    /// </summary>
    private void BuildVerticalCurtainMesh()
    {
        _meshVertices.Clear();
        _meshTriangles.Clear();
        _meshColors.Clear();

        int count = _pointBuffer.Count;

        // 填充幕布顶部 (轨迹中心) 与 底部 (地面投影) 顶点与带透明度顶点色
        for (int i = 0; i < count; i++)
        {
            PathPoint pt = _pointBuffer[i];
            _meshVertices.Add(pt.CenterPos); // 顶点索引: 2 * i (空中)
            _meshVertices.Add(pt.GroundPos);  // 顶点索引: 2 * i + 1 (地面)

            Color baseColor = EvaluatePointColor(pt);

            // 顶部颜色：若是进近刻度线则明显加深高亮
            Color topColor = baseColor;
            topColor.a = pt.IsMilestoneTick ? 0.80f : 0.35f;

            // 底部颜色：淡雅消隐，形成纵深全息光幕效果
            Color btmColor = baseColor;
            btmColor.a = pt.IsMilestoneTick ? 0.50f : 0.08f;

            _meshColors.Add(topColor);
            _meshColors.Add(btmColor);
        }

        // 构建双面幕布三角形
        for (int i = 0; i < count - 1; i++)
        {
            int top0 = 2 * i;
            int btm0 = 2 * i + 1;
            int top1 = 2 * (i + 1);
            int btm1 = 2 * (i + 1) + 1;

            // ---- 正面三角形 ----
            _meshTriangles.Add(top0);
            _meshTriangles.Add(top1);
            _meshTriangles.Add(btm0);

            _meshTriangles.Add(btm0);
            _meshTriangles.Add(top1);
            _meshTriangles.Add(btm1);

            // ---- 反面三角形 ----
            _meshTriangles.Add(top0);
            _meshTriangles.Add(btm0);
            _meshTriangles.Add(top1);

            _meshTriangles.Add(btm0);
            _meshTriangles.Add(btm1);
            _meshTriangles.Add(top1);
        }

        _curtainMesh.Clear();
        _curtainMesh.indexFormat = IndexFormat.UInt32;
        _curtainMesh.SetVertices(_meshVertices);
        _curtainMesh.SetColors(_meshColors);
        _curtainMesh.SetTriangles(_meshTriangles, 0);
        _curtainMesh.RecalculateBounds();
    }

    // =========================================================================
    // 5. 飞行状态多段渐变顶点色解算
    // =========================================================================
    private Color EvaluatePointColor(PathPoint pt)
    {
        if (colorMode == ColorGradientMode.GroundSpeed)
        {
            // 地速渐变映射 (m/s): 0~50 冰蓝 -> 50~130 翠绿 -> 130~250 亮黄 -> >250 艳红
            float spd = pt.Speed;
            if (spd < 50f)
                return Color.Lerp(colorLowSpeed, colorNormalSpeed, Mathf.Clamp01(spd / 50f));
            if (spd < 130f)
                return Color.Lerp(colorNormalSpeed, colorHighSpeed, Mathf.Clamp01((spd - 50f) / 80f));
            return Color.Lerp(colorHighSpeed, colorMaxSpeed, Mathf.Clamp01((spd - 130f) / 120f));
        }
        else
        {
            // 垂直升降速度映射 (m/s)
            float vs = pt.VerticalSpeed;
            if (vs < 0f)
            {
                // 下滑或极速俯冲: 0 (平飞绿) 至 -15 m/s (危险下沉红)
                return Color.Lerp(colorNormalSpeed, colorMaxSpeed, Mathf.Clamp01(-vs / 15f));
            }
            else
            {
                // 爬升: 0 (平飞绿) 至 +20 m/s (大迎角爬升亮黄)
                return Color.Lerp(colorNormalSpeed, colorHighSpeed, Mathf.Clamp01(vs / 20f));
            }
        }
    }

    // =========================================================================
    // 6. 按键交互与零资源材质运行时构建
    // =========================================================================
    private void HandleKeyboardShortcuts()
    {
        // F8: 循环切换轨迹显示模式 (Off -> Ribbon -> Ribbon+Curtain -> SingleLine)
        if (Input.GetKeyDown(KeyCode.F8))
        {
            int nextMode = ((int)currentMode + 1) % 4;
            currentMode = (DisplayMode)nextMode;
            UpdateRendererVisibility();
        }

        // F9: 一键清空所有历史轨迹
        if (Input.GetKeyDown(KeyCode.F9))
        {
            ClearPath();
        }
    }

    public void ClearPath()
    {
        _pointBuffer.Clear();
        _hasFirstSample = false;
        if (_ribbonMesh != null) _ribbonMesh.Clear();
        if (_curtainMesh != null) _curtainMesh.Clear();
    }

    private void InitializeRenderers()
    {
        // 查找支持顶点颜色与透明度的着色材质 (适配 URP 及 Built-in 管线)
        Material baseMat = null;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");

        if (shader != null)
        {
            baseMat = new Material(shader);
        }
        else
        {
            // 防御机制 (应对 Unity 6 URP 独立发布版的 Shader Stripping):
            // 遍历运行时已加载的材质，寻找原生经过 URP 深度认证的 Unlit/Particle 材质克隆
            Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
            if (mats != null && mats.Length > 0)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m != null && m.shader != null)
                    {
                        string sName = m.shader.name;
                        if (sName.Contains("Unlit") || sName.Contains("Sprite") || sName.Contains("Particle"))
                        {
                            baseMat = new Material(m);
                            break;
                        }
                    }
                }
            }
        }

        if (baseMat == null)
        {
            // 最终兜底材质
            baseMat = new Material(Shader.Find("Mobile/Particles/Alpha Blended"));
            if (baseMat == null) baseMat = new Material(Shader.Find("Diffuse"));
        }

        _proceduralMaterial = baseMat;
        _proceduralMaterial.name = "XPlane_FlightPath_ProceduralMat";
        _proceduralMaterial.hideFlags = HideFlags.DontSave;

        // 关键管线配置：完全双面渲染、关闭深度写入 (避免半透明透视穿帮)、开启深度测试
        if (_proceduralMaterial.HasProperty("_Cull"))
            _proceduralMaterial.SetInt("_Cull", (int)CullMode.Off);
        if (_proceduralMaterial.HasProperty("_ZWrite"))
            _proceduralMaterial.SetInt("_ZWrite", 0);
        if (_proceduralMaterial.HasProperty("_ZTest"))
            _proceduralMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);

        // 1. 初始化机翼实体飘带容器
        _ribbonContainer = new GameObject("FlightPath_RibbonContainer");
        _ribbonContainer.hideFlags = HideFlags.DontSave;
        _ribbonContainer.transform.SetParent(this.transform, false);
        _ribbonFilter = _ribbonContainer.AddComponent<MeshFilter>();
        _ribbonRenderer = _ribbonContainer.AddComponent<MeshRenderer>();
        _ribbonMesh = new Mesh();
        _ribbonMesh.name = "Ribbon_Mesh";
        _ribbonFilter.mesh = _ribbonMesh;
        _ribbonRenderer.material = _proceduralMaterial;
        _ribbonRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _ribbonRenderer.receiveShadows = false;

        // 2. 初始化垂直高度投影幕布容器
        _curtainContainer = new GameObject("FlightPath_CurtainContainer");
        _curtainContainer.hideFlags = HideFlags.DontSave;
        _curtainContainer.transform.SetParent(this.transform, false);
        _curtainFilter = _curtainContainer.AddComponent<MeshFilter>();
        _curtainRenderer = _curtainContainer.AddComponent<MeshRenderer>();
        _curtainMesh = new Mesh();
        _curtainMesh.name = "Curtain_Mesh";
        _curtainFilter.mesh = _curtainMesh;
        _curtainRenderer.material = _proceduralMaterial;
        _curtainRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _curtainRenderer.receiveShadows = false;

        UpdateRendererVisibility();
    }

    private void UpdateRendererVisibility()
    {
        bool enableRibbon = (currentMode == DisplayMode.RibbonOnly || 
                             currentMode == DisplayMode.RibbonAndCurtain || 
                             currentMode == DisplayMode.SingleLine);
        bool enableCurtain = (currentMode == DisplayMode.RibbonAndCurtain);

        if (_ribbonRenderer != null) _ribbonRenderer.enabled = enableRibbon;
        if (_curtainRenderer != null) _curtainRenderer.enabled = enableCurtain;
    }
}
