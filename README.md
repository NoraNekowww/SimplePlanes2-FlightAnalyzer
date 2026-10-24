# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

适用于 **SimplePlanes 2** 的通用飞行分析与航电辅助插件（基于 BepInEx 5 构建）。提供专业纯净的着陆参数战报、高频实时动态 G 力示波器，以及对标 **X-Plane 12** 的**独立 URP CommandBuffer 深度光栅化 3D 飞行历史航迹线（Shapes 3D Wireline）**。

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

### 2. 实时 G 力动态变化示波器（快捷键 F7 ）
- **50Hz 物理高频采样**：屏幕右上角显示半透明微型飞行测试示波器，动态滚动绘制最近 3 秒真实 G 力受力波形；
- **标准标尺网格**：清晰标注 `1.0G (平飞亮绿线)`、`2.0G (黄色警戒线)`、`3.0G (红色极限线)`；
- **触地垂线标记**：接地瞬间在曲线上自动打下一根垂直红色标记线，起落架受压冲击波形一目了然。

### 3. X-Plane 12 风格 3D 飞行历史航迹线（快捷键 F6 ）
- **核心调查结论与冲突根除**：
  1. **按键冲突根除**：此前中括号键 `[` / `]` 为 SimplePlanes 2 原生已绑定的 `Previous Target / Next Target`（目标循环切换），导致误触发。现全面改用完全闲置无占用的 **`F7`（示波器）** 与 **`F6`（3D航迹线）**；
  2. **物理受力绿线根除**：此前代码误将 `FlightGizmos.Visible = true` 触发，导致开启了游戏自带的密集物理/浮力受力调试绿线。现**彻底与 FlightGizmos 物理调试系统解耦**，绝不触发全机密集绿线；
  3. **独立 URP CommandBuffer**：直接挂载主相机 `RenderPipelineManager.beginCameraRendering` 生命周期，在透明 Pass 阶段通过 `Shapes.Draw.Command` 直接绘制纯净细腻的天蓝色 3D 矢量线。完全参与硬件 Z-Buffer 深度测试，地景可遮挡航迹、航迹可遮挡远山；
  4. **浮动原点绝对同步（FloatingOrigin Sync）**：自动监听 `FloatingOriginScript.Repositioned` 事件，世界坐标突变时对所有点位执行微米级反向补偿，绝不漂移错位。
- **线状外观与专业质感**：
  - 纯正立体线状（Wireline），设定为 **0.35米真实空间粗细**（Billboard Geometry + Round EndCaps）；
  - 渐变天蓝荧光色系，早期航迹半透明，机尾附近高亮；
- **整局全量航迹（免清除设计）**：
  - 航迹缓冲池容纳 **4000 个航迹线段点**（完整记录整局起降过程）；
  - 空间位移 > 1.2 米自动延伸航迹线，静止滑跑不堆叠；
  - 记录起飞滑跑、爬升转弯、五边进近 3° 下滑线到接地拉平的全套立体航迹线。

---

## ⌨️ 快捷键速查表 (绝对无冲突设计)

彻底淘汰容易引发 Windows 菜单挂起（锁死油门轴）的 `Alt` 键，以及与游戏原生功能（如目标切换、起落架、暂停等）冲突的按键。采用完全闲置、零误触的专用功能键：

| 快捷键 | 功能说明 | 默认状态 |
| :---: | :--- | :---: |
| **`F7`** | 开启 / 关闭屏幕右上角**实时 G 力波形示波器** | 默认关闭，单键随时开关 |
| **`F6`** | 开启 / 关闭 X-Plane 12 风格 **Shapes 3D 矢量航迹线** | 默认关闭，一键展开整局航迹 |

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
