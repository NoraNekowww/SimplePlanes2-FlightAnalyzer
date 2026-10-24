# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

适用于 **SimplePlanes 2** 的通用飞行分析与航电辅助插件（基于 BepInEx 5 构建）。提供专业纯净的着陆参数战报、高频实时动态 G 力示波器，以及对标 **X-Plane 12** 的**程序化三维四边形航迹网格（Procedural 3D Ribbon Mesh）**。

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

### 3. X-Plane 12 风格 3D 飞行历史航迹带（快捷键 ] ）
- **真正参与 3D 空间光栅化**：
  - 彻底淘汰不可靠的 `LineRenderer`，改用底层的 **程序化多边形网格（Procedural 3D Ribbon Mesh）**；
  - 与地景、跑道、飞机机身走相同的顶点着色、几何光栅化与硬件 Z-Buffer 深度测试，无论在任何视角下都绝对真实可见；
  - 动态图层（Layer）与飞机关联，彻底根除相机剔除与近裁剪面穿透导致的开屏蓝屏问题；
- **整局全量航迹（免清除设计）**：
  - 航迹缓冲池扩容至 **5000 个航迹截面点**（支持长达 6~10 分钟整局航线）；
  - 完整记录起飞滑跑、爬升转弯、五边进近下滑道到接地拉平的全套立体航迹线；
- **自适应空间采样与 0 内存开销**：
  - 空间位移 > 1.2 米自动延伸航迹截面，地面静止或慢滑绝不堆叠重复多边形；
  - 航迹带总宽 1.0 米，支持双面光栅化，近大远小清晰夺目。

---

## ⌨️ 快捷键速查表 (绝对无冲突设计)

彻底淘汰容易引发 Windows 菜单挂起（锁死油门轴）的 `Alt` 键，以及游戏原生的 `P`（暂停）、`G`（收放起落架）等按键。改用键盘右侧完全闲置、零误触的专业功能单键：

| 快捷键 | 功能说明 | 默认状态 |
| :---: | :--- | :---: |
| **`[`** (左中括号) | 开启 / 关闭屏幕右上角**实时 G 力波形示波器** | 默认关闭，单键随时开关 |
| **`]`** (右中括号) | 开启 / 关闭 X-Plane 12 风格 **3D 飞行历史航迹带** | 默认关闭，一键展开整局航迹 |

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
