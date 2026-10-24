# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

A general-purpose flight analysis and avionics telemetry plugin for **SimplePlanes 2** (powered by BepInEx 5 Mono x64). Features high-clarity landing rate reports, a realtime G-force waveform oscilloscope, and **game-native Shapes 3D anti-aliased vector trajectory lines** (X-Plane 12-style wireline).

[中文文档 (Chinese)](README.md)

---

## 🌟 Key Features

### 1. Clean & Focused Landing Rate HUD
- **Dual Big-Number Focus**:
  - **SINK RATE**: `30pt` bold font with rating-colored highlights (e.g. `-142 FPM (0.72 m/s)`);
  - **TOUCHDOWN SPEED**: `30pt` bold font in bright white (e.g. `125 KTS (232 km/h)`);
  - **Precise Attitude (PITCH & ROLL)**: Directly bound to game-native quaternion solver, completely eliminating Euler angle gimbal lock and coupling artifacts (e.g. `PITCH +3.8° | ROLL -0.2°`);
- **Focus on What Matters**: Excludes unrealistic rigid-spring G-forces from landing cards, focusing purely on authentic touchdown sink rate and speed;
- **Automatic Pop-up**: Automatically pops up upon touchdown for 7.5 seconds, totally silent during flight with zero manual hotkey needed.

### 2. Realtime G-Force Waveform Oscilloscope (Key: [ )
- **50Hz Physical Sampling**: Top-right corner floating scope plotting 3 seconds of continuous physical G-load;
- **Reference Grid**: Labeled lines for `1.0G (level flight green)`, `2.0G (warning yellow)`, and `3.0G (limit red)`;
- **Touchdown Marker**: Automatically drops a vertical red touchdown marker line upon wheel contact.

### 3. X-Plane 12-Style Shapes 3D Vector Flight Trajectory (Key: ] )
- **Root Cause of Previous Invisibility**:
  - SimplePlanes 2 runs on Unity 6 with a strictly locked URP (`UniversalRendererData`). Independent release builds strip out unreferenced shaders, and any newly instantiated materials without registered URP Passes are dropped by the GPU renderer;
  - Deep inspection of the game engine assemblies revealed that SP2 natively integrates the renowned vector rendering package **`Shapes`** (`ShapesRuntime.dll`), with a registered `ShapesRenderPass` hooked into the main camera's render pipeline;
  - **Native Shapes.Polyline Integration**: Directly utilizes the game's own `Shapes.Polyline` component, rendering crisp, anti-aliased, 3D wireframe trajectory lines with proper depth testing and zero camera-culling issues.
- **Full-Session Continuous Recording**:
  - Stores up to **4,000 waypoint nodes** (covers the entire flight session without needing any manual reset);
  - Seamlessly records takeoff, climb, traffic patterns, 3° approach glideslope, and flare touchdown;
- **Adaptive Distance Sampling & Zero-GC**:
  - Adds waypoints only when displacement > 1.2m, preventing redundant point accumulation while taxiing;
  - 0.35m vector thickness with round joint smoothing, providing high-precision trajectory aesthetics from any cockpit or external view.

---

## ⌨️ Shortcut Keys (Zero-Conflict Design)

Eliminated Windows-menu-trapping `Alt` combinations and native game keys (like `P` for pause and `G` for landing gear). Upgraded to clean, dedicated single keys on the right side of the keyboard:

| Key | Description | Default State |
| :---: | :--- | :---: |
| **`[`** (LeftBracket) | Toggle Realtime **G-Force Waveform Scope** | Disabled by default |
| **`]`** (RightBracket) | Toggle X-Plane 12-style **Shapes 3D Vector Trajectory Line** | Disabled by default |

> **Customizable**: You can customize these keybindings in `SimplePlanes 2/BepInEx/config/com.noraneko.sp2.landingrate.cfg`.

---

## 📥 Installation

### Prerequisites
- **SimplePlanes 2** (Steam version, 64-bit Mono);
- **BepInEx 5 (Mono x64)** (v5.4.20 or newer).

### Quick Install
1. Download the latest `SimplePlanes2-FlightAnalyzer-vX.X.X.zip` from [Releases](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases);
2. Extract the zip archive and copy the `BepInEx` folder directly into your SimplePlanes 2 game root folder (where `SimplePlanes 2.exe` resides);
3. Ensure the plugin DLL is located at:
   ```text
   SimplePlanes 2/
   └── BepInEx/
       └── plugins/
           └── LandingRateMonitor/
               └── LandingRateMonitor.dll
   ```
4. Start the game via Steam and enjoy!

---

## 🛠️ Building from Source

Zero external build tools needed—Windows built-in .NET Framework compiles it out of the box:

```powershell
# 1. Clone repository
git clone https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer.git
cd SimplePlanes2-FlightAnalyzer

# 2. Build and install to your local game directory
.\build.ps1 -InstallToGame

# Or specify a custom game path
.\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\SimplePlanes 2" -InstallToGame
```
Build output is generated at `artifacts/LandingRateMonitor.dll`.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## ⚠️ AI Declaration

> This project was developed under the guidance of the author, utilizing Nous Research Hermes and AI Agent collaboration for reverse engineering, physics optimization, and delivery.
