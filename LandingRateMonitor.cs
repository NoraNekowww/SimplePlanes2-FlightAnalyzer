using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Shapes; // 对接 SimplePlanes 2 官方内置的 URP 矢量渲染管线
using Assets.Scripts.Flight;
using Assets.Scripts.Craft;
using Assets.Scripts.Craft.Parts.Modifiers;

namespace SimplePlanes2.LandingRate
{
    [BepInPlugin("com.noraneko.sp2.landingrate", "Landing Rate & Flight Analyzer", "3.6.0")]
    public class LandingRateMonitor : BaseUnityPlugin
    {
        // 快捷键配置 (绝对无冲突单键: [ = G力示波器, ] = 3D航迹线)
        private ConfigEntry<KeyCode> _keyScope;   // 默认 [: 开启/关闭实时 G 力波形示波器
        private ConfigEntry<KeyCode> _keyPath;    // 默认 ]: 开启/关闭 3D 飞行历史航迹线
        private ConfigEntry<float> _hudDuration;

        // 官方原生私有方法高速委托
        private delegate float FastGetFloat(AircraftScript craft);
        private static FastGetFloat _nativeGetVerticalG;
        private static FastGetFloat _nativeGetGForce;
        private static bool _delegatesInitialized = false;

        // 战机与组件缓存
        private AircraftScript _aircraft;
        private IWheelPart[] _cachedWheels;
        private int _wheelCount = 0;
        private int _lastSearchFrame = -200;

        // 状态机
        private bool _isArmed = false;
        private float _airborneTime = 0f;
        private const float MIN_AIRBORNE_SECONDS = 1.6f;
        private const float MIN_ARM_ALTITUDE_AGL = 3.2f;

        // 垂直下沉率缓冲
        private readonly float[] _vsRing = new float[4];
        private int _ringIndex = 0;

        // 实时遥测
        private float _liveNativeVertG = 1.0f;
        private float _liveNativeGForce = 1.0f;
        private float _liveFpm = 0f;
        private float _liveAgl = 0f;
        private bool _liveGrounded = false;

        // =========================================================================
        // 核心技术突破：对接 SimplePlanes 2 官方原生 Shapes.Polyline 矢量画线通道
        // 1. Shapes 已完全内建注册在当前游戏相机的 UniversalRendererData 中 (ShapesRenderPass)
        // 2. 100% 确保参与硬件深度测试，与地景飞机共同在原生 Pass 光栅化，彻底解决隐形与穿透
        // 3. 原生 3D 抗锯齿纯线状 (Wire / Line Trajectory)，对标 X-Plane 12 专业质感
        // =========================================================================
        private bool _showFlightPath = false;
        private GameObject _shapesHostObj = null;
        private Shapes.Polyline _shapesPolyline = null;

        private const int MAX_WAYPOINTS = 4000;
        private readonly List<PolylinePoint> _pointsList = new List<PolylinePoint>(MAX_WAYPOINTS);
        private Vector3 _lastSampledPos = Vector3.zero;
        private const float MIN_SAMPLE_DISTANCE_SQ = 1.44f; // 空间位移 > 1.2 米自动记录一个新线段节点

        // =========================================================================
        // 模块二：高精度实时 G 力示波器 (按 [ 自由开关)
        // =========================================================================
        public struct ScopeSample
        {
            public float rawG;
            public bool isTouchdown;
        }

        private const int SCOPE_CAPACITY = 140;
        private readonly ScopeSample[] _scopeBuffer = new ScopeSample[SCOPE_CAPACITY];
        private int _scopeHead = 0;
        private bool _showWaveformScope = false; // 默认关闭，按 [ 随时开启

        // =========================================================================
        // 模块三：着陆结算卡片 (触地自动弹出 7.5 秒，无需手动按键，纯净大字)
        // =========================================================================
        public struct LandingRecord
        {
            public float fpm;
            public float mps;
            public float kts;
            public float kmh;
            public float pitch;
            public float roll;
            public string ratingTitle;
            public Color accentColor;

            public string strFpmMain;
            public string strFpmSub;
            public string strSpeedMain;
            public string strSpeedSub;
            public string strAttitude;
        }

        private LandingRecord _report;
        private float _displayTimer = 0f;
        private float _totalDuration = 7.5f;

        // 静默预热控制
        private int _warmupFramesLeft = 2;

        // UI 资源
        private Texture2D _roundedCardTex;
        private Texture2D _accentTex;
        private Texture2D _pixelWhite;
        private Texture2D _scopeBgTex;

        private GUIStyle _cardBgStyle;
        private GUIStyle _accentBadgeStyle;
        private GUIStyle _badgeTextStyle;
        private GUIStyle _bigNumberStyle;
        private GUIStyle _unitStyle;
        private GUIStyle _fieldLabelStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _scopeTitleStyle;
        private GUIStyle _scopeLegendStyle;

        private void Awake()
        {
            // 绑定绝对无冲突的右侧单键
            _keyScope = Config.Bind("Controls", "KeyGForceScope", KeyCode.LeftBracket, "实时 G 力示波器开关快捷键 (默认左中括号 [)");
            _keyPath = Config.Bind("Controls", "KeyFlightPath", KeyCode.RightBracket, "3D 飞行历史轨迹线开关快捷键 (默认右中括号 ])");
            _hudDuration = Config.Bind("UI", "DisplayDuration", 7.5f, "着陆结算卡片停留时间(秒)");
            _totalDuration = _hudDuration.Value;

            InitNativeDelegates();
            PrebakeUIAssets();
            PrecompileJIT();

            // 初始化示波器缓冲
            for (int i = 0; i < SCOPE_CAPACITY; i++)
            {
                _scopeBuffer[i] = new ScopeSample { rawG = 1.0f, isTouchdown = false };
            }

            Logger.LogInfo("[LandingRateMonitor v3.6.0] 官方 Shapes 原生抗锯齿矢量航迹系统已就绪: [ = 示波器, ] = 3D真实航迹线。");
        }

        private static void InitNativeDelegates()
        {
            if (_delegatesInitialized) return;
            try
            {
                Type craftType = typeof(AircraftScript);
                MethodInfo mVertG = craftType.GetMethod("get_VerticalG", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mVertG != null)
                {
                    _nativeGetVerticalG = (FastGetFloat)Delegate.CreateDelegate(typeof(FastGetFloat), mVertG);
                }

                MethodInfo mGForce = craftType.GetMethod("get_GForce", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mGForce != null)
                {
                    _nativeGetGForce = (FastGetFloat)Delegate.CreateDelegate(typeof(FastGetFloat), mGForce);
                }

                _delegatesInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LandingRateMonitor] 绑定原生 GForce 失败: " + ex.Message);
            }
        }

        private void PrebakeUIAssets()
        {
            int size = 64;
            int r = 14;
            float borderThickness = 1.2f;

            _roundedCardTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _roundedCardTex.filterMode = FilterMode.Bilinear;
            _roundedCardTex.wrapMode = TextureWrapMode.Clamp;

            Color fillColor = new Color(0.04f, 0.07f, 0.12f, 0.94f);
            Color strokeColor = new Color(0.35f, 0.48f, 0.65f, 0.50f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = GetRoundedBoxDistance(x, y, size, size, r);
                    if (dist > 0.5f)
                    {
                        _roundedCardTex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist > -borderThickness)
                    {
                        float edgeAlpha = Mathf.Clamp01(0.5f - dist);
                        _roundedCardTex.SetPixel(x, y, new Color(strokeColor.r, strokeColor.g, strokeColor.b, strokeColor.a * edgeAlpha));
                    }
                    else
                    {
                        _roundedCardTex.SetPixel(x, y, fillColor);
                    }
                }
            }
            _roundedCardTex.Apply(false, true);

            int badgeH = 24;
            int badgeW = 48;
            _accentTex = new Texture2D(badgeW, badgeH, TextureFormat.RGBA32, false);
            _accentTex.filterMode = FilterMode.Bilinear;
            _accentTex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < badgeH; y++)
            {
                for (int x = 0; x < badgeW; x++)
                {
                    float dist = GetRoundedBoxDistance(x, y, badgeW, badgeH, 7);
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    _accentTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            _accentTex.Apply(false, true);

            _pixelWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelWhite.SetPixel(0, 0, Color.white);
            _pixelWhite.Apply(false, true);

            _scopeBgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _scopeBgTex.SetPixel(0, 0, new Color(0.02f, 0.05f, 0.09f, 0.92f));
            _scopeBgTex.Apply(false, true);

            _cardBgStyle = new GUIStyle();
            _cardBgStyle.border = new RectOffset(r + 2, r + 2, r + 2, r + 2);
            _cardBgStyle.normal.background = _roundedCardTex;

            _accentBadgeStyle = new GUIStyle();
            _accentBadgeStyle.border = new RectOffset(8, 8, 8, 8);
            _accentBadgeStyle.normal.background = _accentTex;

            _badgeTextStyle = new GUIStyle();
            _badgeTextStyle.fontSize = 13;
            _badgeTextStyle.fontStyle = FontStyle.Bold;
            _badgeTextStyle.alignment = TextAnchor.MiddleCenter;

            _bigNumberStyle = new GUIStyle();
            _bigNumberStyle.fontSize = 30;
            _bigNumberStyle.fontStyle = FontStyle.Bold;
            _bigNumberStyle.alignment = TextAnchor.MiddleRight;

            _unitStyle = new GUIStyle();
            _unitStyle.fontSize = 10;
            _unitStyle.fontStyle = FontStyle.Bold;
            _unitStyle.normal.textColor = new Color(0.65f, 0.75f, 0.88f);
            _unitStyle.alignment = TextAnchor.LowerLeft;

            _fieldLabelStyle = new GUIStyle();
            _fieldLabelStyle.fontSize = 11;
            _fieldLabelStyle.fontStyle = FontStyle.Bold;
            _fieldLabelStyle.normal.textColor = new Color(0.55f, 0.65f, 0.78f);
            _fieldLabelStyle.alignment = TextAnchor.MiddleLeft;

            _footerStyle = new GUIStyle();
            _footerStyle.fontSize = 12;
            _footerStyle.normal.textColor = new Color(0.75f, 0.82f, 0.90f);
            _footerStyle.alignment = TextAnchor.MiddleCenter;

            _scopeTitleStyle = new GUIStyle();
            _scopeTitleStyle.fontSize = 11;
            _scopeTitleStyle.fontStyle = FontStyle.Bold;
            _scopeTitleStyle.normal.textColor = new Color(0.35f, 0.85f, 1.0f);
            _scopeTitleStyle.alignment = TextAnchor.MiddleLeft;

            _scopeLegendStyle = new GUIStyle();
            _scopeLegendStyle.fontSize = 10;
            _scopeLegendStyle.normal.textColor = new Color(0.70f, 0.80f, 0.92f);
            _scopeLegendStyle.alignment = TextAnchor.MiddleRight;
        }

        // =========================================================================
        // 关键组件初始化：使用游戏官方内建的 Shapes.Polyline 矢量渲染器
        // =========================================================================
        private void EnsureShapesPolylineRenderer()
        {
            if (_shapesHostObj != null && _shapesPolyline != null) return;

            try
            {
                _shapesHostObj = new GameObject("SP2_NativeShapesTrajectory");

                // 挂载在飞机世界父容器下，跟随 FloatingOrigin 浮动原点绝对同步
                if (_aircraft != null)
                {
                    _shapesHostObj.layer = _aircraft.gameObject.layer;
                    Transform container = (_aircraft.transform.parent != null) ? _aircraft.transform.parent : _aircraft.transform;
                    _shapesHostObj.transform.SetParent(container, false);
                }

                _shapesPolyline = _shapesHostObj.AddComponent<Shapes.Polyline>();
                
                // 线状外观：0.35米真实 3D 空间矢量线宽，抗锯齿圆角连接
                _shapesPolyline.Thickness = 0.35f;
                _shapesPolyline.ThicknessSpace = ThicknessSpace.Meters;
                _shapesPolyline.Color = new Color(0.18f, 0.85f, 1.0f, 0.90f); // 航空天蓝荧光
                _shapesPolyline.Geometry = PolylineGeometry.Billboard;        // 3D 空间全向可见立体线
                _shapesPolyline.Joins = PolylineJoins.Round;                  // 平滑圆角过渡
                _shapesPolyline.ZTest = UnityEngine.Rendering.CompareFunction.LessEqual; // 参与真实深度遮挡测试

                _shapesPolyline.enabled = true;
                Logger.LogInfo("[LandingRateMonitor] 官方 Shapes.Polyline 3D 矢量航迹线已成功挂载！");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LandingRateMonitor] 初始化 Shapes.Polyline 失败: " + ex.Message);
            }
        }

        private void PrecompileJIT()
        {
            try
            {
                _report = new LandingRecord();
                BuildReportStrings(ref _report, 110f, 55f, 3.2f, 0.1f);
                _report.ratingTitle = "";
            }
            catch {}
        }

        private float GetRoundedBoxDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - (w - 1) * 0.5f) - ((w - 1) * 0.5f - r);
            float qy = Mathf.Abs(y - (h - 1) * 0.5f) - ((h - 1) * 0.5f - r);
            float mx = Mathf.Max(qx, 0f);
            float my = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, my), 0f) - r;
        }

        private void Update()
        {
            // 1. 按 [ (LeftBracket): G 力动态示波器开关
            if (Input.GetKeyDown(_keyScope.Value))
            {
                _showWaveformScope = !_showWaveformScope;
            }

            // 2. 按 ] (RightBracket): 3D 飞行历史航迹线开关 (开启/隐藏整局全景)
            if (Input.GetKeyDown(_keyPath.Value))
            {
                _showFlightPath = !_showFlightPath;
                if (_showFlightPath)
                {
                    EnsureShapesPolylineRenderer();
                    if (_shapesPolyline != null)
                    {
                        _shapesPolyline.enabled = true;
                        PushAllPointsToPolyline();
                    }
                }
                else if (_shapesPolyline != null)
                {
                    _shapesPolyline.enabled = false;
                }
            }

            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.deltaTime;
            }
        }

        private void FixedUpdate()
        {
            if (_aircraft == null || !_aircraft.gameObject.activeInHierarchy || !_aircraft.IsPrimaryLocalPlayer)
            {
                if (Time.frameCount - _lastSearchFrame > 40)
                {
                    _lastSearchFrame = Time.frameCount;
                    FindAndCachePlayerAircraft();
                }
                if (_aircraft == null) return;
            }

            // 1. 物理采样
            Vector3 currentVel = _aircraft.Velocity;
            float dt = Time.fixedDeltaTime;

            float vertSpeedMps = currentVel.y;
            float currentDownFpm = -vertSpeedMps * 196.8504f;

            _ringIndex = (_ringIndex + 1) & 3;
            _vsRing[_ringIndex] = currentDownFpm;

            float agl = _aircraft.AltitudeAgl;
            bool isGrounded = CheckCraftGroundedFast();

            _liveNativeVertG = _nativeGetVerticalG != null ? _nativeGetVerticalG(_aircraft) : 1.0f;
            if (float.IsNaN(_liveNativeVertG) || float.IsInfinity(_liveNativeVertG)) _liveNativeVertG = 1.0f;

            _liveNativeGForce = _nativeGetGForce != null ? _nativeGetGForce(_aircraft) : 1.0f;
            if (float.IsNaN(_liveNativeGForce) || float.IsInfinity(_liveNativeGForce)) _liveNativeGForce = 1.0f;

            _liveFpm = currentDownFpm;
            _liveAgl = agl;
            _liveGrounded = isGrounded;

            bool isTouchdownEvent = false;

            // 2. 升空武装逻辑
            if (!isGrounded && agl > MIN_ARM_ALTITUDE_AGL)
            {
                _airborneTime += dt;
                if (_airborneTime >= MIN_AIRBORNE_SECONDS)
                {
                    _isArmed = true;
                }
            }
            else if (isGrounded)
            {
                if (_isArmed)
                {
                    isTouchdownEvent = true;

                    int prevIdx = (_ringIndex + 3) & 3;
                    float touchdownFpm = _vsRing[prevIdx];
                    if (touchdownFpm < 0f) touchdownFpm = 0f;

                    TriggerLanding(touchdownFpm);
                    _isArmed = false;
                    _airborneTime = 0f;
                }
            }

            // 3. 示波器压入样本
            _scopeHead = (_scopeHead + 1) % SCOPE_CAPACITY;
            _scopeBuffer[_scopeHead] = new ScopeSample
            {
                rawG = Mathf.Abs(_liveNativeVertG),
                isTouchdown = isTouchdownEvent
            };

            // 4. 连续记录整局飞行航迹线段点
            SampleTrajectoryPolylinePoint();
        }

        private void SampleTrajectoryPolylinePoint()
        {
            if (_aircraft == null) return;

            Vector3 centerPos = _aircraft.transform.position;

            if (_lastSampledPos == Vector3.zero)
            {
                _lastSampledPos = centerPos;
                return;
            }

            float distSq = (centerPos - _lastSampledPos).sqrMagnitude;

            // 空间位移超过 1.2 米自动记录一个新线段节点
            if (distSq >= MIN_SAMPLE_DISTANCE_SQ)
            {
                _lastSampledPos = centerPos;

                Vector3 localPoint = centerPos;
                if (_shapesHostObj != null && _shapesHostObj.transform.parent != null)
                {
                    localPoint = _shapesHostObj.transform.parent.InverseTransformPoint(centerPos);
                }

                // 节点色彩带微弱渐变
                Color ptColor = new Color(0.20f, 0.85f, 1.0f, 0.90f);
                PolylinePoint pt = new PolylinePoint(localPoint, ptColor);

                if (_pointsList.Count < MAX_WAYPOINTS)
                {
                    _pointsList.Add(pt);
                }
                else
                {
                    _pointsList.RemoveAt(0);
                    _pointsList.Add(pt);
                }

                // 若当前处于开启状态，向 Polyline 增量追加节点
                if (_showFlightPath && _shapesPolyline != null && _pointsList.Count >= 2)
                {
                    _shapesPolyline.enabled = true;
                    _shapesPolyline.SetPoints(_pointsList);
                }
            }
        }

        private void PushAllPointsToPolyline()
        {
            if (_shapesPolyline == null || _pointsList.Count < 2) return;
            _shapesPolyline.SetPoints(_pointsList);
        }

        private bool CheckCraftGroundedFast()
        {
            if (_cachedWheels != null)
            {
                for (int i = 0; i < _wheelCount; i++)
                {
                    IWheelPart w = _cachedWheels[i];
                    if (w != null && w.IsGrounded)
                    {
                        return true;
                    }
                }
            }

            if (_aircraft.AltitudeAgl <= 0.40f)
            {
                return true;
            }

            return false;
        }

        private void TriggerLanding(float touchdownFpm)
        {
            float pitch = _aircraft.InstrumentData.Pitch;
            float roll = _aircraft.InstrumentData.Roll;
            while (roll > 180f) roll -= 360f;
            while (roll < -180f) roll += 360f;

            float speedMps = _aircraft.AirSpeed;
            BuildReportStrings(ref _report, touchdownFpm, speedMps, pitch, roll);

            _displayTimer = _totalDuration;
        }

        private void BuildReportStrings(ref LandingRecord rep, float fpm, float speedMps, float pitch, float roll)
        {
            float speedKts = speedMps * 1.943844f;
            float speedKmh = speedMps * 3.6f;
            float sinkMps = fpm * 0.00508f;

            rep.fpm = fpm;
            rep.mps = sinkMps;
            rep.kts = speedKts;
            rep.kmh = speedKmh;
            rep.pitch = pitch;
            rep.roll = roll;

            if (fpm < 100f)
            {
                rep.ratingTitle = "BUTTER TOUCHDOWN 🧈";
                rep.accentColor = new Color(0.20f, 0.95f, 0.45f);
            }
            else if (fpm <= 220f)
            {
                rep.ratingTitle = "GOOD LANDING ✨";
                rep.accentColor = new Color(0.25f, 0.85f, 1.0f);
            }
            else if (fpm <= 380f)
            {
                rep.ratingTitle = "FIRM LANDING ✈";
                rep.accentColor = new Color(1.0f, 0.85f, 0.25f);
            }
            else if (fpm <= 550f)
            {
                rep.ratingTitle = "HARD LANDING ⚠";
                rep.accentColor = new Color(1.0f, 0.50f, 0.15f);
            }
            else
            {
                rep.ratingTitle = "SLAM IMPACT 💥";
                rep.accentColor = new Color(1.0f, 0.22f, 0.25f);
            }

            rep.strFpmMain = "-" + rep.fpm.ToString("F0");
            rep.strFpmSub = "(" + rep.mps.ToString("F2") + " m/s)";

            rep.strSpeedMain = rep.kts.ToString("F0");
            rep.strSpeedSub = "(" + rep.kmh.ToString("F0") + " km/h)";

            rep.strAttitude = string.Format("PITCH {0:+0.0;-0.0}°   |   ROLL {1:+0.0;-0.0}°",
                rep.pitch, rep.roll);
        }

        private void FindAndCachePlayerAircraft()
        {
            AircraftScript found = null;
            if (FlightSceneScript.Instance != null && FlightSceneScript.Instance.LocalPlayer != null)
            {
                found = FlightSceneScript.Instance.LocalPlayer.Aircraft;
            }

            if (found == null)
            {
                AircraftScript[] crafts = FindObjectsOfType<AircraftScript>();
                for (int i = 0; i < crafts.Length; i++)
                {
                    AircraftScript c = crafts[i];
                    if (c != null && c.IsPrimaryLocalPlayer)
                    {
                        found = c;
                        break;
                    }
                }
            }

            if (found != null)
            {
                _aircraft = found;

                if (_shapesHostObj != null)
                {
                    _shapesHostObj.layer = found.gameObject.layer;
                }

                List<IWheelPart> wList = found.WheelParts;
                if (wList != null)
                {
                    _cachedWheels = wList.ToArray();
                    _wheelCount = _cachedWheels.Length;
                }
                else
                {
                    _cachedWheels = null;
                    _wheelCount = 0;
                }
            }
        }

        private void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color textColor)
        {
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);

            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
        }

        private void PerformWarmUpPass()
        {
            Color oldColor = GUI.color;
            GUI.color = Color.clear;

            Rect offscreen = new Rect(-3000, -3000, 10, 10);
            GUI.Label(offscreen, "BUTTER GOOD FIRM HARD SLAM 🧈 ✨ ✈ ⚠ 💥", _badgeTextStyle);
            GUI.Label(offscreen, "-1234567890 + . FPM KTS IAS km/h PITCH ROLL | °", _bigNumberStyle);
            GUI.Box(offscreen, GUIContent.none, _cardBgStyle);
            GUI.Box(offscreen, GUIContent.none, _accentBadgeStyle);
            GUI.DrawTexture(offscreen, _pixelWhite);

            GUI.color = oldColor;
        }

        private void RenderGForceWaveformScope()
        {
            int scopeW = 290;
            int scopeH = 145;
            int scopeX = Screen.width - scopeW - 20;
            int scopeY = 25;

            Color origColor = GUI.color;

            // 1. 示波器底板
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(scopeX, scopeY, scopeW, scopeH), _scopeBgTex);

            DrawShadowLabel(new Rect(scopeX + 10, scopeY + 6, 140, 16),
                "G-LOAD SCOPE [ [ ]", _scopeTitleStyle, _scopeTitleStyle.normal.textColor);

            string legend = string.Format("V/S:{0,4:F0} | LIVE:<color={1}>+{2:F2}G</color>",
                _liveFpm, _liveNativeVertG > 2.0f ? "#FFAA33" : "#55FF55", _liveNativeVertG);
            DrawShadowLabel(new Rect(scopeX + 100, scopeY + 6, scopeW - 110, 16),
                legend, _scopeLegendStyle, Color.white);

            int plotX = scopeX + 28;
            int plotY = scopeY + 26;
            int plotW = scopeW - 38;
            int plotH = scopeH - 36;

            float yFor0G = plotY + plotH;
            float yFor1G = plotY + plotH * (1f - 1.0f / 3.0f);
            float yFor2G = plotY + plotH * (1f - 2.0f / 3.0f);
            float yFor3G = plotY;

            GUI.color = new Color(0.20f, 0.30f, 0.45f, 0.40f);
            GUI.DrawTexture(new Rect(plotX, yFor0G, plotW, 1), _pixelWhite);
            GUI.DrawTexture(new Rect(plotX, yFor2G, plotW, 1), _pixelWhite);

            GUI.color = new Color(0.35f, 0.70f, 0.50f, 0.70f);
            GUI.DrawTexture(new Rect(plotX, yFor1G, plotW, 1), _pixelWhite);

            GUI.color = Color.white;
            GUIStyle tickStyle = new GUIStyle(_scopeLegendStyle);
            tickStyle.fontSize = 9;
            tickStyle.alignment = TextAnchor.MiddleRight;
            DrawShadowLabel(new Rect(scopeX + 2, yFor3G - 6, 24, 12), "3G", tickStyle, new Color(1f, 0.4f, 0.4f));
            DrawShadowLabel(new Rect(scopeX + 2, yFor2G - 6, 24, 12), "2G", tickStyle, new Color(1f, 0.8f, 0.3f));
            DrawShadowLabel(new Rect(scopeX + 2, yFor1G - 6, 24, 12), "1G", tickStyle, new Color(0.5f, 1f, 0.6f));
            DrawShadowLabel(new Rect(scopeX + 2, yFor0G - 6, 24, 12), "0G", tickStyle, new Color(0.6f, 0.7f, 0.8f));

            int sampleCount = Mathf.Min(plotW / 2, SCOPE_CAPACITY);
            int startIdx = (_scopeHead - sampleCount + 1 + SCOPE_CAPACITY) % SCOPE_CAPACITY;

            for (int i = 0; i < sampleCount; i++)
            {
                int curIdx = (startIdx + i) % SCOPE_CAPACITY;
                ScopeSample s = _scopeBuffer[curIdx];

                float xPos = plotX + i * 2;

                if (s.isTouchdown)
                {
                    GUI.color = new Color(1f, 0.35f, 0.35f, 0.90f);
                    GUI.DrawTexture(new Rect(xPos, plotY, 2, plotH), _pixelWhite);
                }

                float val = s.rawG;
                if (float.IsNaN(val) || float.IsInfinity(val)) val = 1.0f;
                float clampedG = Mathf.Clamp(val, 0f, 3.0f);
                float barY = plotY + plotH * (1f - clampedG / 3.0f);

                Color col;
                if (clampedG >= 2.0f)
                    col = new Color(1f, 0.45f, 0.2f);
                else if (clampedG >= 1.4f)
                    col = new Color(1f, 0.85f, 0.25f);
                else
                    col = new Color(0.25f, 0.90f, 0.45f);

                GUI.color = col;
                GUI.DrawTexture(new Rect(xPos, barY - 1, 2, 2), _pixelWhite);
            }

            GUI.color = origColor;
        }

        private void OnGUI()
        {
            if (_warmupFramesLeft > 0)
            {
                PerformWarmUpPass();
                _warmupFramesLeft--;
                return;
            }

            Color rootColor = GUI.color;

            // 1. 实时 G 力示波器 (按 [ 自由开关)
            if (_showWaveformScope && _aircraft != null)
            {
                RenderGForceWaveformScope();
            }

            // 2. 着陆战报大字卡片 (触地全自动弹出，平时完全静默，无需按键)
            if (_displayTimer > 0f && !string.IsNullOrEmpty(_report.ratingTitle))
            {
                float alpha = 1.0f;
                if (_displayTimer < 0.8f)
                {
                    alpha = Mathf.Clamp01(_displayTimer / 0.8f);
                }
                else if (_totalDuration - _displayTimer < 0.25f)
                {
                    alpha = Mathf.Clamp01((_totalDuration - _displayTimer) / 0.25f);
                }

                GUI.color = new Color(1f, 1f, 1f, alpha);

                int cardW = 340;
                int cardH = 138;
                int cardX = (Screen.width - cardW) / 2;
                int cardY = 45;

                Rect cardRect = new Rect(cardX, cardY, cardW, cardH);

                // 1. 磨砂深蓝灰抗锯齿圆角底板 (9-Slice)
                GUI.Box(cardRect, GUIContent.none, _cardBgStyle);

                // 2. 顶栏：居中评级徽标胶囊
                int badgeW = 150;
                int badgeH = 22;
                int badgeX = cardX + (cardW - badgeW) / 2;
                int badgeY = cardY + 10;

                GUI.color = new Color(_report.accentColor.r, _report.accentColor.g, _report.accentColor.b, alpha * 0.25f);
                GUI.Box(new Rect(badgeX, badgeY, badgeW, badgeH), GUIContent.none, _accentBadgeStyle);

                GUI.color = new Color(1f, 1f, 1f, alpha);
                DrawShadowLabel(new Rect(badgeX, badgeY, badgeW, badgeH), _report.ratingTitle, _badgeTextStyle, _report.accentColor);

                // 3. 核心双焦点数据排布区 (下沉率 + 空速)
                int contentY = cardY + 38;
                int colW = (cardW - 36) / 2;
                int col1X = cardX + 18;
                int col2X = col1X + colW + 8;

                // --- 左焦点：下沉率 (SINK RATE) ---
                DrawShadowLabel(new Rect(col1X, contentY, colW, 14), "SINK RATE", _fieldLabelStyle, new Color(0.60f, 0.70f, 0.85f));
                DrawShadowLabel(new Rect(col1X, contentY + 12, colW - 38, 38), _report.strFpmMain, _bigNumberStyle, _report.accentColor);
                DrawShadowLabel(new Rect(col1X + colW - 36, contentY + 28, 36, 16), "FPM", _unitStyle, new Color(0.65f, 0.75f, 0.88f));

                // --- 右焦点：接地空速 (TOUCHDOWN SPEED) ---
                DrawShadowLabel(new Rect(col2X, contentY, colW, 14), "TOUCHDOWN SPEED", _fieldLabelStyle, new Color(0.60f, 0.70f, 0.85f));
                DrawShadowLabel(new Rect(col2X, contentY + 12, colW - 34, 38), _report.strSpeedMain, _bigNumberStyle, Color.white);
                DrawShadowLabel(new Rect(col2X + colW - 32, contentY + 28, 32, 16), "KTS", _unitStyle, new Color(0.65f, 0.75f, 0.88f));

                // 4. 底部微型半透明分割线
                GUI.color = new Color(0.30f, 0.40f, 0.55f, alpha * 0.40f);
                GUI.DrawTexture(new Rect(cardX + 20, cardY + 96, cardW - 40, 1), _pixelWhite);

                // 5. 底部姿态角行 (Pitch & Roll)
                GUI.color = new Color(1f, 1f, 1f, alpha);
                DrawShadowLabel(new Rect(cardX, cardY + 104, cardW, 16), _report.strAttitude, _footerStyle, _footerStyle.normal.textColor);

                // 6. 底部倒计时进度条
                float remainProgress = Mathf.Clamp01(_displayTimer / _totalDuration);
                GUI.color = new Color(_report.accentColor.r, _report.accentColor.g, _report.accentColor.b, alpha * 0.75f);
                GUI.DrawTexture(new Rect(cardX + 16, cardY + cardH - 3, (cardW - 32) * remainProgress, 2), _pixelWhite);
            }

            GUI.color = rootColor;
        }

        private void OnDestroy()
        {
            if (_roundedCardTex != null) Destroy(_roundedCardTex);
            if (_accentTex != null) Destroy(_accentTex);
            if (_pixelWhite != null) Destroy(_pixelWhite);
            if (_scopeBgTex != null) Destroy(_scopeBgTex);

            if (_shapesHostObj != null) Destroy(_shapesHostObj);
        }
    }
}
