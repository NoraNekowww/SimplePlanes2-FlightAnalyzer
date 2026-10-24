# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

A general-purpose flight analysis and avionics telemetry plugin for **SimplePlanes 2** (powered by BepInEx 5 Mono x64). Focuses on providing clean, professional landing rate reports and a high-frequency realtime G-force waveform oscilloscope.

[中文文档 (Chinese)](README.md) | [AI Handoff Documentation](AI_HANDOFF.md)

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

### 3. X-Plane 12 Style 3D Flight Path (Keys: F8 / F9 )
- **Wing Ribbon**: Procedural 3D solid mesh reflecting true bank (Roll) and climb/sink (Pitch), rendered with double-sided Quad Strips for zero backface culling;
- **Vertical Approach Curtain**: Realtime downward Raycast to terrain generating a translucent vertical curtain with periodic high-contrast milestone ticks for realistic glideslope depth;
- **Dynamic Vertex Color**: Color-mapped dynamically against true airspeed / ground speed (Cyan -> Green -> Yellow -> Red) with zero texture asset overhead;
- **Floating Origin Shift Protection**: Deeply hooked into `FloatingOriginScript` with delta-jump compensation, ensuring long cross-country tracks never skew or tear.

---

## ⌨️ Shortcut Keys (Zero-Conflict Design)

Eliminated Windows-menu-trapping `Alt` combinations and native game keys (like target switching `[` / `]`, landing gear, and pause):

| Key | Description | Default State |
| :---: | :--- | :---: |
| **`F7`** | Toggle Realtime **G-Force Waveform Scope** | Disabled by default |
| **`F8`** | Cycle **3D Flight Path Mode** (Off -> Ribbon -> Ribbon+Curtain -> SingleLine) | Enabled by default (Ribbon+Curtain) |
| **`F9`** | Clear all recorded 3D flight trajectory points | Instant reset |
| *Auto* | **Landing Rate Report HUD** (Sink Rate + Speed + Pitch/Roll) | Pops up automatically on touchdown for 7.5s |

> **Configuration**: Customizable in `SimplePlanes 2/BepInEx/config/com.noraneko.sp2.landingrate.cfg`.

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
