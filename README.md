# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

适用于 **SimplePlanes 2** 的通用飞行分析与航电辅助插件（基于 BepInEx 5 构建）。提供专业纯净的着陆参数战报、高频实时动态 G 力示波器，以及对标 **X-Plane 12** 的**游戏原生 URP 深度光栅化 3D 飞行历史航迹线（Shapes 3D Wireline）**。

[English Documentation](README.en.md)

---

## 🌟 核心特性

### 1. 纯净航空着陆战报 HUD（Landing Rate Monitor）
- **超大字双焦点展示**：
  - **下沉率 (SINK RATE)**：`30pt` 超大粗体高亮，支持英制主单位与公制副单位（如 `-142 FPM (0.72 m/s)`），与评级动态色彩联动；
  - **接地空速 (TOUCHDOWN SPEED)**：`30pt` 超大粗体（如 `125 KTS (232 km/h)`）；
  - **精密接地姿态 (PITCH & ROLL)**：严格对接游戏原生航空四元数解算，完全消除欧拉角万向节死锁（如 `PITCH +3.8° | ROLL -0.2°`）；
- **剔除非真实 G 力干扰**：聚焦真正核心的接地下沉率、触地速度与精确姿态，战报纯净专业；
- **全自动弹出**：接地触地时中央自动淡入弹出保持 7.5 秒，平时完全静默，无需手动按键干扰。

### 2. 实时 G 力动态变化示波器（快捷键 [ ）
- **50Hz 物理高频采样**：屏幕右上角显示半透明微型飞行测试示波器，动态滚动绘制最近 3 秒真实 G 力受力波形；
- **标准标尺网格**：清晰标注 `1.0G (平飞亮绿线)`、`2.0G (黄色警戒线)`、`3.0G (红色极限线)`；
- **触地垂线标记**：接地瞬间在曲线上自动打下一根垂直红色标记线，起落架受压冲击波形一目了然。

### 3. X-Plane 12 风格 3D 飞行历史航迹线（快捷键 ] ）
- **为什么此前无论哪种方法都不可见？（图形管线底层调查实录）**
  1. **URP 渲染管线限制**：SimplePlanes 2 基于 Unity 6 的 Universal Render Pipeline 构建。独立发布版本开启了严格的 Shader Stripping（着色器打包剔除），普通外部加载的材质没有合法的 UniversalForward Pass，会被 GPU 批处理直接剔除；
  2. **官方隐藏画线通道确认**：经深度反编译与 IL 逆向，SimplePlanes 2 官方工程内建并重度使用了业界著名的高性能矢量引擎 **`Shapes`**（`ShapesRuntime.dll`）；
  3. **官方 CommandBuffer 机制**：官方所有 3D 辅助线（如机翼升力中心、发动机推力线、悬挂调试线）均实现 `Assets.Scripts.Flight.UI.IFlightGizmo` 接口，并在主相机的 `RenderPipelineManager.beginCameraRendering` 生命周期中通过 `Shapes.Draw.Command(cam, RenderPassEvent.AfterRenderingTransparents)` 批量压入 GPU 渲染队列；
  4. **彻底解决**：本插件接入 `IFlightGizmo` 并向 `FlightSceneScript.FlightGizmos` 注册，同时挂载 URP `beginCameraRendering` 原生通道，通过官方 `Shapes.Draw.Line` 直接在经过 URP 授权的 Pass 提交抗锯齿 3D 矢量线。**完全参与硬件 Z-Buffer 深度测试，地景可遮挡航迹、航迹可遮挡远山，100% 真实光栅化到屏幕上！**
- **线状外观与专业质感**：
  - 纯正立体线状（Wireline），设定为 **0.35米真实空间粗细**（Billboard Geometry + Round EndCaps）；
  - 渐变天蓝荧光色系，早期航迹半透明，机尾附近高亮；
- **整局全量航迹（免清除设计）**：
  - 航迹缓冲池容纳 **4000 个航迹线段点**（完整记录 6~10 分钟整局起降过程）；
  - 空间位移 > 1.2 米自动延伸航迹线，静止滑跑不堆叠；
  - 记录起飞滑跑、爬升转弯、五边进近 3° 下滑线到接地拉平的全套立体航迹线。

---

## ⌨️ 快捷键速查表 (绝对无冲突设计)

彻底淘汰容易引发 Windows 菜单挂起（锁死油门轴）的 `Alt` 键，以及游戏原生的 `P`（暂停）、`G`（收放起落架）等按键。改用键盘右侧完全闲置、零误触的专业功能单键：

| 快捷键 | 功能说明 | 默认状态 |
| :---: | :--- | :---: |
| **`[`** (左中括号) | 开启 / 关闭屏幕右上角**实时 G 力波形示波器** | 默认关闭，单键随时开关 |
| **`]`** (右中括号) | 开启 / 关闭 X-Plane 12 风格 **Shapes 3D 矢量航迹线** | 默认关闭，一键展开整局航迹 |

> **自由改键**：你可以在 `SimplePlanes 2/BepInEx/config/com.noraneko.sp2.landingrate.cfg` 配置文件中随意将按键修改为你习惯的任意单键或手柄按键。

---

## 📥 安装方法

### 前置要求
- **SimplePlanes 2**（Steam 版本，64 位 Mono 架构）；
- **BepInEx 5 (Mono x64)**（版本 $\ge$ 5.4.20）。

### 一键安装
1. 前往 [Releases](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases) 下载最新的 `SimplePlanes2-FlightAnalyzer-vX.X.X.zip`；
2. 解压压缩包，将里面的 `BepInEx` 文件夹直接复制并覆盖到你的 SimplePlanes 2 游戏根目录下（即包含 `SimplePlanes 2.exe` 的目录）；
3. 确保最终 DLL 位于：
   ```text
   SimplePlanes 2/
   └── BepInEx/
       └── plugins/
           └── LandingRateMonitor/
               └── LandingRateMonitor.dll
   ```
4. 正常从 Steam 启动游戏即可生效！

---

## 🛠️ 从源码构建

本项目采用零第三方依赖构建设计，Windows 系统自带的 .NET Framework 即可原生直编：

```powershell
# 1. 克隆仓库
git clone https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer.git
cd SimplePlanes2-FlightAnalyzer

# 2. 一键编译并安装到本地游戏目录
.\build.ps1 -InstallToGame

# 或指定自定义游戏路径
.\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\SimplePlanes 2" -InstallToGame
```
编译产物将生成在 `artifacts/LandingRateMonitor.dll`。

---

## 📄 开源许可证

本项目采用 [MIT License](LICENSE) 许可协议。

---

## ⚠️ AI 声明 / AI Declaration

> 本项目由开发者指导并借助 Nous Research Hermes 与 AI Agent 协同生成、逆向调优与全流程交付。
