# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

A general-purpose flight analysis and avionics telemetry plugin for **SimplePlanes 2** (powered by BepInEx 5 Mono x64). Features high-clarity landing rate reports, a realtime G-force waveform oscilloscope, and **isolated URP CommandBuffer 3D wireline flight trajectories** (X-Plane 12-style wireline).

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

### 2. Realtime G-Force Waveform Oscilloscope (Key: F7 )
- **50Hz Physical Sampling**: Top-right corner floating scope plotting 3 seconds of continuous physical G-load;
- **Reference Grid**: Labeled lines for `1.0G (level flight green)`, `2.0G (warning yellow)`, and `3.0G (limit red)`;
- **Touchdown Marker**: Automatically drops a vertical red touchdown marker line upon wheel contact.

### 3. X-Plane 12-Style 3D Vector Flight Trajectory (Key: F6 )
- **Root Cause & Conflict Elimination**:
  1. **Key Conflict Resolved**: Square bracket keys `[` / `]` were natively assigned to `Previous Target / Next Target` in SimplePlanes 2. Upgraded to dedicated and unused **`F7` (Scope)** and **`F6` (3D Trajectory)**;
  2. **Physical Vector Clutter Eliminated**: Previous versions unintentionally triggered `FlightGizmos.Visible = true`, which opened the game's internal dense green aerodynamic and buoyancy vector debug arrows. Now completely **decoupled from FlightGizmos**;
  3. **Isolated URP CommandBuffer**: Directly hooks into the main camera's `RenderPipelineManager.beginCameraRendering` lifecycle, rendering a pure cyan-blue 3D wireline via `Shapes.Draw.Command` during the transparent pass. Completely participates in hardware depth testing (`ZTest.LessEqual`), ensuring seamless rasterization with terrain and aircraft;
  4. **Floating Origin Synchronized**: Hooks into `FloatingOriginScript.Repositioned` to provide sub-millimeter reverse offsets whenever the world coordinates shift, keeping the flight path permanently anchored to runways and terrain.
- **Wireline Aesthetics**:
  - Clean 3D wireline geometry with **0.35m thickness** (`Billboard Geometry` + `Round EndCaps`);
  - Smooth cyan-blue gradient with fading tail and bright leading tip;
- **Full-Session Continuous Recording**:
  - Stores up to **4,000 waypoint segments** (covers the entire flight session without needing any manual reset);
  - Seamlessly records takeoff, climb, traffic patterns, 3° approach glideslope, and flare touchdown;
  - Adaptive sampling (displacement > 1.2m) avoids redundant vertex stacking while parked or taxiing.

---

## ⌨️ Shortcut Keys (Zero-Conflict Design)

Eliminated Windows-menu-trapping `Alt` combinations and native game keys (like target switching, landing gear, and pause). Upgraded to clean, dedicated function keys:

| Key | Description | Default State |
| :---: | :--- | :---: |
| **`F7`** | Toggle Realtime **G-Force Waveform Scope** | Disabled by default |
| **`F6`** | Toggle X-Plane 12-style **3D Wireline Trajectory** | Disabled by default |

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
