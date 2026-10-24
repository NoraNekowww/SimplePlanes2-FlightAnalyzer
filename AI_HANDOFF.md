# SimplePlanes 2 - 插件开发与技术交接文档 (AI Handoff Documentation)

> **文档定位**：本文件专供后续接手开发本项目的 AI Agent 或开发者使用，详尽记录了 SimplePlanes 2 逆向成果、图形与物理架构细节、历史踩坑实录以及 3D 轨迹线未来实现的突破口。

---

## 目录
1. [项目全景与开发环境](#1-项目全景与开发环境)
2. [已完全跑通并保留的核心功能](#2-已完全跑通并保留的核心功能)
3. [SimplePlanes 2 底层架构与逆向成果](#3-simpleplanes-2-底层架构与逆向成果)
4. [3D 轨迹线（Flight Trajectory）历史探索实录与失败复盘](#4-3d-轨迹线历史探索实录与失败复盘)
5. [后续 AI 实现 3D 轨迹线的明确技术路线推荐](#5-后续-ai-实现-3d-轨迹线的明确技术路线推荐)
6. [按键与交互设计禁忌（必读）](#6-按键与交互设计禁忌必读)
7. [仓库管理与编译交付流水线](#7-仓库管理与编译交付流水线)

---

## 1. 项目全景与开发环境

- **项目名称**：SimplePlanes2-FlightAnalyzer (Landing Rate & Flight Analyzer)
- **开源仓库**：`https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer`
- **本地源码工作区**：`E:\Agent_Workspace_Isolated\Throttle_Project\SimplePlanes2_LandingRate`
- **目标游戏**：SimplePlanes 2 (Steam 早期发布版本)
  - 游戏根目录：`E:\SteamLibrary\steamapps\common\SimplePlanes 2`
  - 核心执行体：`SimplePlanes 2.exe` (Unity 6 / 6000.3 Mono x64 架构)
  - 插件框架：BepInEx 5 (Mono x64, 路径 `BepInEx\plugins\LandingRateMonitor\LandingRateMonitor.dll`)
- **构建环境与工具链**：
  - Windows 原生 C# 编译器：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
  - 编译脚本：`build.ps1 -InstallToGame`（0 第三方包依赖，直接引用游戏 `Managed` 目录下的 DLL）

---

## 2. 已完全跑通并保留的核心功能

当前插件版本为 **v4.0.0**，代码完全精简收敛，稳定运行：

### 2.1 纯净航空着陆战报 HUD (Landing Rate Report)
- **自动触发逻辑**：飞机离地起飞且真高（AGL）> 3.2m 持续 1.6 秒后进入武装状态（Armed）；接地瞬间锁定第 0 帧垂直速度，居中淡入弹出 **7.5 秒** 后自动淡出，平时 100% 保持完全静默，无需玩家手动按键。
- **两大双焦点大字**：
  - `SINK RATE`：30pt 超大字，以 `-FPM` 为主，附带 `(m/s)`；根据接地质量联动色彩（Butter: 绿色, Good: 蓝色, Firm: 黄色, Hard: 橙色, Slam: 红色）；
  - `TOUCHDOWN SPEED`：30pt 亮白大字，以 `KTS` 为主，附带 `(km/h)`；
  - `ATTITUDE`：精确接地姿态角 `PITCH +X.X° | ROLL -X.X°`。
- **G 力剥离原则**：SP2 底层物理悬挂为线性刚弹簧，接地瞬间动能刹停会导致 G 力偏硬虚高（正常 200 FPM 接地计算瞬态 G 可达 2.4G）。因此**战报中严禁混入 G 力**，还原真实飞行员只关注下沉率、速度与姿态的纯净原则。
- **抗锯齿与预热**：采用 9-Slice SDF 圆角卡片，并在关卡加载前两帧执行透明字形预热（Prewarm），彻底消除了 Unity FreeType 在首帧渲染 Emoji 时的卡顿掉帧。

### 2.2 实时 G 力波形示波器 (Oscilloscope)
- **默认快捷键**：**`F7`** 单键开关（按键设置中未被占用，无冲突）。
- **参数特性**：右上角 140 样本 50Hz 物理滚动采样，带 `1.0G (平飞绿线)`、`2.0G (警戒黄线)`、`3.0G (极限红线)` 标尺网格；触地瞬间自动打下红色垂直标记线。

---

## 3. SimplePlanes 2 底层架构与逆向成果

通过反编译 `Game.dll` 及游戏运行时程序集，确立了以下关键底层事实：

1. **玩家飞机单例定位**：
   ```csharp
   AircraftScript craft = FlightSceneScript.Instance.LocalPlayer.Aircraft;
   ```
   *注意：切勿在场景中按 `Rigidbody.mass` 查找最大刚体，否则会误锁至数万吨的航母单例 `AircraftCarrierScript`！*
2. **接地判定（Grounded）**：
   ```csharp
   List<IWheelPart> wheels = craft.WheelParts;
   // 遍历 wheels[i].IsGrounded，并以 craft.AltitudeAgl <= 0.40f 进行低空兜底
   ```
3. **姿态与速度源**：
   - 空速：`craft.AirSpeed` (单位为 m/s，换算 KTS 乘以 `1.943844f`，换算 km/h 乘以 `3.6f`)；
   - 姿态角：直接取原生四元数解算结果 `craft.InstrumentData.Pitch` 与 `craft.InstrumentData.Roll`；
   - *Roll 必须做模周期转换消除死锁*：`(roll + 180f) % 360f - 180f`。
4. **原生 G 力私有方法**：
   `AircraftScript` 内部包含非公开方法 `get_VerticalG` 与 `get_GForce`，已通过反射预先生成高速开放委托 `FastGetFloat`，零开销直接调用。
5. **浮动原点系统（Floating Origin）**：
   SP2 是大地图游戏，当飞机远离世界原点时会触发 `Assets.Scripts.Flight.FloatingOriginScript.Instance.Repositioned` 事件，全局物体坐标会瞬移 `e.Delta`。

---

## 4. 3D 轨迹线历史探索实录与失败复盘

用户期望类似 **X-Plane 12** 的 3D 飞行历史航迹线（细线状、常驻保留整局飞行）。历史开发中尝试了 3 种方案，均遇到特殊阻碍，现记录如下以供下一任 AI 避坑：

### 失败尝试 1：传统 `LineRenderer` 组件
- **现象**：线条在 3D 空间中完全不可见；在开局原点生成时，因顶点未填充或被座舱相机近裁剪面穿透，甚至引发整屏大面积蓝屏。
- **原因**：SP2 在 Unity 6 URP 独立打包时开启了严格的 Shader Stripping。外部通过 `Shader.Find()` 创建的材质因没有在 UniversalRendererData 中注册 Pass，被 URP GPU 批处理直接剔除。

### 失败尝试 2：`Procedural Ribbon Mesh`（程序化四边形带状网格）
- **现象**：虽然带状多边形理论上参与光栅化，但克隆游戏机身材质仍未能在 URP 的 Transparent/Opaque Pass 中顺利投射；且用户明确指出**偏好细线状（Wireline）**，不希望是宽平面的缎带。

### 失败尝试 3：接入游戏内置的 `Shapes.Polyline` / `Shapes.Draw.Command`
- **现象**：
  1. 此前代码误将 `FlightGizmos.Visible = true` 开启，导致直接触发了 SimplePlanes 2 原生全局物理调试模式，**飞机机身与机翼全部布满了密密麻麻的绿色受力竖线和红色顶针**（见实机截图），画面严重被毁！
  2. 随后尝试直接在 `RenderPipelineManager.beginCameraRendering` 中调用 `Shapes.Draw.Line`，但由于相机的 CommandBuffer 上下文或 RenderPassEvent 未能匹配 SP2 自身相机的内部渲染管线，导致线条仍然未能投射。

---

## 5. v4.1.0 现已完全跑通的 X-Plane 12 风格 3D 飞行历史轨迹 (FlightPathRecorder)

在 **v4.1.0** 中，已成功攻克 3D 轨迹渲染的技术瓶颈，采用高内聚的 **Procedural Mesh** 方案实现了原汁原味的《X-Plane 12》飞行姿态与高度下滑道双层渲染：

### 5.1 核心架构与技术突破
1. **机翼姿态飘带（Wing Ribbon）**：
   - 根据飞机实时世界坐标、四元数姿态与设定翼展（`ribbonWingspan`），动态解算左右翼端点。
   - 采用 Quad Strip 算法，构建**正反两套三角形索引（双面渲染）**，彻底消灭飞机大坡度盘旋、横滚倒飞时的背面剔除（Backface Culling）隐形问题。
2. **地面高度下滑道幕布（Vertical Curtain）**：
   - 飞机中心向下物理 Raycast 探测真实地形高度（未命中则优雅投射至海平面 Y=0）。
   - 每隔 N 个采样点（默认 10）打下高亮加深的垂直刻度线（Milestone Tick），幕布底部半透明渐隐，完美还原 X-Plane 12 全息进近下滑道视觉纵深。
3. **URP Shader Stripping 防御机制**：
   - 运行时探测 Shader：优先 `Universal Render Pipeline/Unlit`，次选 `Sprites/Default`、`Universal Render Pipeline/Particles/Unlit` 等。
   - **兜底克隆**：若独立发布版将 Shader 剥离，自动通过 `Resources.FindObjectsOfTypeAll<Material>()` 从游戏已加载资源中提取带顶点色的原生合法材质作为模板 Clone，确保 100% 参与 URP 光栅化渲染。
4. **浮动原点（Floating Origin）双重容灾补偿**：
   - **原生事件**：启动时自动反射订阅 `Assets.Scripts.Flight.FloatingOriginScript.Instance.Repositioned`，捕获 `e.Delta` 并在世界原点重置时平移已记录的所有历史坐标。
   - **自适应检测**：在 `LateUpdate` 中差分对比帧间位移，扣除飞机物理惯性速度推算值后，一旦发现跳变超过 500 米，自动对所有历史坐标施加反向平移补偿，杜绝轨迹拉扯。
5. **智能采样与 32 位定容缓冲**：
   - 基于位移（`dist >= 3.0m`）或姿态旋转（`angle >= 5.0°`）触发采样，消除停机滑行时的冗余顶点。
   - 开启 `mesh.indexFormat = IndexFormat.UInt32`，结合 5000 点定容滑动窗口（Sliding Window FIFO），零 GC 内存顿挫。

### 5.2 交互快捷键（Zero-Conflict 原则）
- **`F8`**：循环切换显示模式（关闭 -> 仅机翼飘带 -> 飘带+地面高度幕布 -> 仅单线轨迹）。
- **`F9`**：一键清空当前已记录的所有历史轨迹点。
- **`F7`**：实时 G 力示波器开关（沿用 v4.0.0）。

---

## 6. 按键与交互设计禁忌（必读）

下一任 AI 在设计快捷键时，必须严格遵守以下准则：

1. **绝对禁忌中括号 `[` 与 `]`**：
   - **血泪教训**：在 SP2 官方原生默认 Controls 设置中，**`[` 为 Previous Target，`]` 为 Next Target**（见设置截图）。使用中括号会导致玩家在飞行时被强制切换目标或陷入锁敌模式！
2. **绝对禁忌 `Alt` 组合键**：
   - 单独按下或松开 `Alt` 会触发 Windows 系统的菜单栏焦点捕获，导致 Unity 丢失轴输入，**将飞机油门轴瞬间锁定在 12% 无法加油门**！
3. **绝对禁忌游戏常用字母单键**：
   - `P`：原生游戏暂停（Pause）；
   - `G`：原生起落架收放（Gear）；
   - `L`：原生着陆灯（Lights）。
4. **推荐按键方案**：
   - 当前版本已统一分配为：**`F7` 开关实时 G 力示波器**；
   - 备用未占用功能键建议：`F6` 或右侧小键盘数字键；
   - 绝不硬编码，统一在 `com.noraneko.sp2.landingrate.cfg` 中提供 BepInEx Config 配置项。

---

## 7. 仓库管理与编译交付流水线

- **自动化编译**：
  ```powershell
  cd "E:\Agent_Workspace_Isolated\Throttle_Project\SimplePlanes2_LandingRate"
  .\build.ps1 -InstallToGame
  ```
- **配置覆盖注意**：
  若修改了 C# 中的默认按键，用户本地已生成的 `BepInEx\config\com.noraneko.sp2.landingrate.cfg` 仍会优先读取旧键值。如果需要强制生效新默认值，在 `build.ps1` 或安装逻辑中应提醒玩家删除该 `.cfg` 文件重新生成。
- **GitHub 同步与发布**：
  由于本地网络环境，Git push 与 GitHub CLI 须挂载本地 SOCKS5 代理：
  ```bash
  git -c http.proxy=socks5h://127.0.0.1:10808 push origin main
  HTTP_PROXY=socks5h://127.0.0.1:10808 HTTPS_PROXY=socks5h://127.0.0.1:10808 gh release create vX.X.X ...
  ```

---
*文档编制完成于 2026 年 10 月，SimplePlanes 2 插件体系规范。*
