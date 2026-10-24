# SimplePlanes 2 - Flight Analyzer & Landing Rate Monitor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.4-green.svg)](https://github.com/BepInEx/BepInEx)
[![Release](https://img.shields.io/github/v/release/NoraNekowww/SimplePlanes2-FlightAnalyzer)](https://github.com/NoraNekowww/SimplePlanes2-FlightAnalyzer/releases)

A general-purpose flight analysis and avionics telemetry plugin for **SimplePlanes 2** (powered by BepInEx 5 Mono x64). Features high-clarity landing rate reports, a realtime G-force waveform oscilloscope, and **X-Plane 12-style** 3D flight trajectory path visualization.

[中文文档 (Chinese)](README.md)

---

## 🌟 Key Features

### 1. Clean & Focused Landing Rate HUD
- **Dual Big-Number Focus**:
  - **SINK RATE**: `30pt` bold font with rating-colored highlights (e.g. `-142 FPM (0.72 m/s)`);
  - **TOUCHDOWN SPEED**: `30pt` bold font in bright white (e.g. `125 KTS (232 km/h)`);
  - **Precise Attitude (PITCH & ROLL)**: Directly bound to game-native quaternion solver, completely eliminating Euler angle gimbal lock and coupling artifacts (e.g. `PITCH +3.8° | ROLL -0.2°`);
- **Focus on What Matters**: Excludes unrealistic rigid-spring G-forces from landing cards, focusing purely on authentic touchdown sink rate and speed;
- **Native Jundroo-Style Rounded Glass**: SDF (Signed Distance Field) pre-baked frosted glass dark panel with smooth 14px anti-aliased corners;
- **Zero-Frame Warm-up**: Pre-bakes FreeType glyph atlases and shader passes during the first 2 loading frames, completely eliminating first-touchdown stutter.

### 2. Realtime G-Force Waveform Oscilloscope (Key: [ )
- **50Hz Physical Sampling**: Top-right corner floating scope plotting 3 seconds of continuous physical G-load;
- **Reference Grid**: Labeled lines for `1.0G (level flight green)`, `2.0G (warning yellow)`, and `3.0G (limit red)`;
- **Touchdown Marker**: Automatically drops a vertical red touchdown marker line upon wheel contact.

### 3. X-Plane 12-Style 3D Flight Path Trajectory (Keys: ] / \ )
- **Approach & Pattern Review**: Renders an in-world glowing cyan ribbon trajectory to inspect your 3° glide slope, flare curve, and traffic pattern;
- **Extreme Performance & Camera-Safe**:
  - 100% lazily instantiated on demand—no global objects at spawn time, completely eliminating cockpit camera blue occlusions;
  - Adaptive distance sampling (>1.2m displacement per point) prevents vertex bloat when taxiing or stopped;
  - Ring buffer supporting 2,000 waypoints (covers 6–10 minutes of full approach flights);
  - Zero DrawCalls and zero rendering overhead when toggled off.

---

## ⌨️ Shortcut Keys (Zero-Conflict Design)

Eliminated Windows-menu-trapping `Alt` combinations and native game keys (like `P` for pause and `G` for landing gear). Upgraded to clean, dedicated single keys on the right side of the keyboard:

| Key | Description | Default State |
| :---: | :--- | :---: |
| **`'`** (Quote) | Show / Hide the **Landing Report HUD** | Pops up upon touchdown for 7.5s |
| **`[`** (LeftBracket) | Toggle Realtime **G-Force Waveform Scope** | Disabled by default |
| **`]`** (RightBracket) | Toggle X-Plane 12-style **3D Flight Path Trajectory** | Disabled by default |
| **`\`** (Backslash) | **Clear and reset current flight trajectory path** | Ready anytime |

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
