# ImuGui - IMU Sensor Visualization & Analysis Platform

[![CI](https://github.com/74hamed/ImuGui/actions/workflows/ci.yml/badge.svg)](https://github.com/74hamed/ImuGui/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Tests](https://img.shields.io/badge/tests-150%20passing-brightgreen)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow.svg)](LICENSE)

A comprehensive Windows desktop application for real-time visualization, analysis, and processing of IMU (Inertial Measurement Unit) sensor data with modern 3D graphics, quaternion sensor fusion, and per-channel Kalman filtering.

## 📋 Overview

ImuGui is a professional-grade C# WinForms application designed for engineers, researchers, and developers working with 9-axis IMU sensors. It provides an intuitive interface for monitoring multi-axis accelerometer, gyroscope, and magnetometer data in real time — replayed from a CSV recording or streamed live from a serial (COM) device — with Kalman filtering for noise reduction, interactive OpenGL 3D visualization, aircraft instruments, and orientation tracking.

The application is a ground-up rebuild with a layered, fully-tested architecture: a UI-independent core (sources, filtering, fusion, calibration), an isolated OpenGL rendering layer, owner-drawn instruments, and a dependency-injected WinForms shell. The solution builds with **zero warnings** (warnings are errors) and ships with **150 unit tests** and CI.

## 🎯 Key Features

### 📊 Real-time Data Visualization
- **Multi-sensor Display** - Simultaneous visualization of accelerometer, gyroscope, and magnetometer data plus temperature
- **Live Charts** - Three scrolling time-series charts (gyro / accel / mag) with X/Y/Z series, per-axis visibility toggles, and a raw-signal overlay
- **Bounded History** - Chart memory is a fixed-capacity ring buffer; old points are dropped, memory never grows
- **Numeric Readouts** - Consistently formatted values for every channel plus roll/pitch/yaw
- **Status Indicators** - A connection lamp that reflects the *real* source state (connecting / connected / reconnecting / faulted), measured sample rate, and frame count

### 🎮 3D Graphics Rendering
- **Two Independent Cube Views** - Each driven by a user-selectable quantity (Accelerometer, Magnetometer, Gyroscope, or Orientation — mutually exclusive between the views) with per-view raw/filtered toggles
- **Interactive 3D Environment** - Ground reference grid (toggleable), colored XYZ axes, and the oriented sensor cube
- **Blender-style Camera Control** - Mouse-driven orbit camera with Blender's viewport bindings:
  - **Middle Drag**: Orbit / rotate view
  - **Shift + Middle Drag**: Pan camera
  - **Ctrl + Middle Drag**: Zoom (drag up = closer)
  - **Mouse Wheel**: Zoom in/out
  - **Home**: Reset view to default
- **Artificial Horizon Display** - Owner-drawn attitude indicator driven by roll + pitch
- **Heading Indicator** - Aviation-style rotating compass card driven by yaw
- **Orientation Tracking** - Real-time Roll, Pitch, Yaw (RPY) from quaternion sensor fusion

### 🔧 Signal Processing & Filtering
- **Kalman Filtering** - A 1-D Kalman filter per channel, managed as a keyed filter bank (all 10 channels)
- **Configurable Filter Parameters** (edited live via the tuning dialog, validated input):
  - **Q** - Process noise covariance
  - **R** - Measurement noise covariance
  - **P₀** - Initial estimate error covariance
  - **X₀** - Initial state estimate
- **Explicit Retune Semantics** - Choose to reset filter state or retune smoothly while running
- **Global Raw ⇄ Filtered Toggle** - Switches every readout, chart, gauge, and 3D view consistently
- **Sensor Fusion** - Quaternion **Mahony MARG** filter by default (no gimbal lock), with selectable Euler **complementary** and **Kalman** (two-state, online gyro-bias estimation) filters; correct tilt-compensated magnetometer heading
- **Measured dt** - Fusion integrates the *measured* time between samples — never a hardcoded interval

### 📁 Data Management
- **CSV Replay** - Load a recorded CSV and replay it at a configurable rate, optionally looping; header row auto-detected; malformed rows are skipped and reported, never silently zero-filled
- **Live Serial Input** - Real COM-port acquisition: port enumeration, async background reads, malformed-line tolerance, and automatic reconnect after device loss
- **CsvHelper Integration** - Robust, invariant-culture CSV parsing with an explicit column map
- **Settings Persistence** - Source selection, port/baud, filter tuning, chart preferences, cube selections, and calibration survive restarts (`%AppData%\ImuGui\`)

### ⚙️ Sensor Calibration
- **Gyroscope Bias** - Stationary capture, computed and subtracted from the stream
- **Six-Position Accelerometer** - Per-axis bias and scale from the classic six-face routine, with per-face progress tracking
- **Magnetometer Hard/Soft-Iron** - Figure-eight capture with live min/max coverage display
- **Apply & Persist** - Computed profiles apply to the live stream immediately and persist between runs; calibration can be toggled on/off

## 🛠️ Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Language** | C# | 12 |
| **Framework** | .NET | 8.0 (LTS) |
| **GUI** | Windows Forms | Built-in |
| **Graphics** | OpenTK (OpenGL 3.3 core) | 4.9.4 + GLControl 4.0.2 |
| **Charts** | ScottPlot.WinForms | 5.0.56 |
| **CSV Processing** | CsvHelper | 33.1.0 |
| **Serial I/O** | System.IO.Ports | 9.0.18 |
| **DI / Hosting / Logging** | Microsoft.Extensions.* + Serilog | 8.x |
| **Testing** | xUnit + FluentAssertions | 2.9.3 / 7.2.2 |
| **IDE** | Visual Studio 2022+ or `dotnet` CLI | — |
| **Platform** | Windows Desktop | 10/11 |

## 📦 Dependencies

All dependencies arrive via NuGet with **central package version management** (`Directory.Packages.props`) — no loose DLLs, no `packages.config`, no manual steps:

```
OpenTK + OpenTK.GLControl   3D rendering (isolated in ImuGui.Rendering)
ScottPlot.WinForms          Live charts
CsvHelper                   CSV parsing/replay
System.IO.Ports             Serial acquisition
Microsoft.Extensions.*      Dependency injection, hosting, logging abstractions
Serilog (+ sinks)           Console + rolling-file logging
xUnit / FluentAssertions    Test suite (FluentAssertions pinned to 7.x, the last Apache-2.0 line)
```

To restore dependencies:
```powershell
dotnet restore
```

## 📁 Project Structure

```
ImuGui/
├── src/
│   ├── ImuGui.Core/              # UI-independent core — no graphics references
│   │   ├── Models/               #   SensorSample, Vector3, Quaternion, Orientation
│   │   ├── Sources/              #   ISensorSource, CSV replay, real serial source, line parser
│   │   ├── Filtering/            #   KalmanScalarFilter, FilterBank, FilterConfig
│   │   ├── Fusion/               #   Mahony & complementary estimators, tilt compensation
│   │   ├── Calibration/          #   Gyro/accel/mag calibrators, profile, service
│   │   ├── Pipeline/             #   SensorPipeline → immutable ProcessedFrames
│   │   ├── Cameras/              #   Tested OrbitCamera math
│   │   ├── Collections/          #   Bounded RingBuffer<T>
│   │   └── Abstractions/         #   IClock (injectable time — deterministic tests)
│   ├── ImuGui.Rendering/         # OpenTK 3D views — the ONLY project referencing OpenGL
│   ├── ImuGui.Instruments/       # Owner-drawn artificial horizon & heading indicator
│   └── ImuGui.App/               # WinForms shell: DI bootstrap, MVP presenters, views, dialogs
├── tests/
│   └── ImuGui.Core.Tests/        # 150 xUnit tests over all core logic
├── samples/
│   └── imu-sample.csv            # Bundled 40 s / 50 Hz recording — works out of the box
├── docs/                         # Architecture, data formats, controls reference
├── .github/workflows/ci.yml      # Build + test on windows-latest; warnings fail the build
├── Directory.Build.props         # Shared build settings (nullable, warnings-as-errors)
├── Directory.Packages.props      # Central package versions
├── ImuGui.sln
├── LICENSE                       # MIT
└── README.md                     # This file
```

## 🏗️ Architecture & Components

### Core Components

**SensorPipeline (Processing Seam)**
- Composes calibration → filter bank → two fusion estimators (one fed raw, one fed filtered data)
- Publishes immutable `ProcessedFrame` snapshots carrying both variants
- Measures per-sample dt from timestamps — the raw/filtered toggle is a pure view concern

**ISensorSource (Data Acquisition)**
- One interface, two real implementations: `CsvReplaySensorSource` and `SerialSensorSource`
- Background-thread acquisition with connection-state events and actionable error messages
- Serial: enumeration, open-failure diagnostics listing available ports, auto-reconnect

**FilterBank (Signal Processing)**
- One `KalmanScalarFilter` per channel, keyed by an enum — no duplicated field-per-channel code
- Thread-safe retuning while the stream runs; configuration separated from runtime state

**MahonyOrientationEstimator / ComplementaryOrientationEstimator (Fusion)**
- Quaternion Mahony MARG filter with anti-windup PI correction (default)
- Euler complementary filter with proper Euler-rate kinematics and wrap-aware yaw blending
- Both initialize instantly from the first accelerometer + magnetometer sample

**OrbitCamera (3D Navigation)**
- Pure math, fully unit-tested, with an explicitly initialized rotation center
- Consumed by the environment view; controls match the documentation exactly

**MainPresenter (MVP)**
- All UI-side logic: source lifecycle, toggles, retuning, fusion switching, settings round-trip
- Forms are passive views — no domain math in code-behind

### Data Flow Architecture

```
CSV Recording          Serial IMU Device
      │                       │
      └──────► ISensorSource ◄┘        (background acquisition thread)
                    │
                    ▼
             SensorPipeline
                    │  calibration profile (optional)
                    ├─► FilterBank (10 × Kalman)
                    │         │
              raw path   filtered path
                    │         │
             Mahony/Compl. fusion ×2
                    │
                    ▼
          ProcessedFrame (raw + filtered + both orientations)
                    │
        ┌───────────┼────────────┬───────────────┐
        ▼           ▼            ▼               ▼
   3D Views     Charts      Instruments     Readouts / Status
        (UI render tick ~30 FPS — decoupled from data rate)
```

## 🚀 Getting Started

### Prerequisites

- **Windows 10** or later
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
- **Graphics card** with OpenGL 3.3+ support
- **Serial IMU device** (optional — the bundled CSV recording works with no hardware)

### Installation & Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/74hamed/ImuGui.git
   cd ImuGui
   ```

2. **Build & Test** (CLI)
   ```bash
   dotnet build     # zero warnings, or the build fails
   dotnet test      # 150 tests
   ```

3. **Run the Application**
   ```bash
   dotnet run --project src/ImuGui.App
   ```

   Or open `ImuGui.sln` in Visual Studio 2022+ and press `F5`. A fresh clone builds with no manual steps — NuGet restore is automatic.

### First-Time Setup

1. **Try it with no hardware**
   - Select **CSV replay**, browse to `samples/imu-sample.csv`
   - Click **Connect** — the recording plays at 50 Hz (level rest → roll sweep → pitch sweep → full 360° yaw)
   - Toggle **Use filtered data** and the chart **raw overlay** to see the Kalman effect

2. **Connect real hardware**
   - Plug in your IMU device via USB/serial and click **Refresh** next to the port picker
   - Select the COM port and matching baud rate (default 115200, 8N1; DTR is asserted on open)
   - Click **Connect** — the status lamp turns green only when data actually flows
   - Your device must emit the [serial line protocol](docs/data-formats.md) (10 comma-separated values per line)

3. **Calibrate (recommended)**
   - Open **Calibrate…** and run the three routines (gyro still-capture, six-position accel, magnetometer figure-eight)
   - **Apply & save** — the profile persists and applies to the live stream

## 📊 Sensor Data Format

The application processes standard 9-axis IMU data plus temperature, as an immutable record:

```csharp
public sealed record SensorSample(
    TimeSpan Timestamp,        // monotonic, stamped by the source — dt is measured, never assumed
    Vector3  Gyroscope,        // deg/s (body rates)
    Vector3  Accelerometer,    // g     (specific force; level & stationary = (0, 0, -1))
    Vector3  Magnetometer,     // device units, typically µT
    double   TemperatureCelsius);
```

**Conventions:** NED aerospace frames — body X forward, Y right, Z down; Euler ZYX; yaw is a compass heading (0° = north, clockwise-positive). Full details in [docs/data-formats.md](docs/data-formats.md).

**CSV columns / serial line fields** (identical 10-value contract, invariant culture, header optional in files):

```
GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature
```

## 🎮 User Interface Guide

### Main Window

**Header** (always visible)
- **Connect/Disconnect** button + real-state status lamp
- **Use filtered data** — the global raw/filtered toggle
- **Theme toggle** (☀/🌙, dark by default) and the **⚙ Settings** button

**Settings page** (header gear button)
- **Data source**: CSV replay (file picker, rate, loop) or Serial (COM port + Refresh, baud rate)
- **Processing**: **Tune filters…** dialog, fusion strategy picker (Mahony / complementary / Kalman), **Calibrate…** and apply-calibration toggle

**Navigation**
- **Dashboard**: artificial horizon + heading indicator alongside all numeric readouts
- **Charts**: the three scrolling sensor charts with axis toggles and raw overlay
- **3D Views**: the two selectable cube views
- **Environment**: the interactive grid/axes/cube scene

**Bottom Status Bar**: transient messages (e.g. reconnect attempts), measured sample rate, processed frame count

### 3D View Controls

The bindings follow **Blender's viewport navigation**:

| Input | Action |
|-------|--------|
| **Middle Mouse Drag** | Orbit / rotate view |
| **Shift + Middle Drag** | Pan camera (moves the rotation center) |
| **Ctrl + Middle Drag** | Zoom (drag up = closer) |
| **Mouse Wheel Up** | Zoom in |
| **Mouse Wheel Down** | Zoom out |
| **Home** (or **R**) | Reset view to default |
| **Reset camera button** | Same as **Home** |

These bindings are documented in [docs/controls.md](docs/controls.md) and backed by the tested `OrbitCamera` — the on-screen hint bar shows the same mapping the code implements.

### Chart Visualization

- **X-axis**: Time (scrolling window, configurable 2–120 s)
- **Y-axis**: Sensor values (auto-scaled)
- **Multiple Series**: X/Y/Z in distinct colors, individually toggleable
- **Toggle Options**:
  - Filtered data (Kalman) — when the global toggle is on
  - Raw data — when it's off
  - **Overlay raw** — faint raw traces on top of filtered ones for direct comparison

## 🔧 Kalman Filtering Configuration

Open **Tune filters…** to edit the parameters applied to all channels. Input is validated (invariant culture, `.` decimal separator) and applied atomically to the whole bank:

```csharp
// Defaults
Q  = 0.001;   // Process noise — how much the true value drifts between samples
R  = 0.1;     // Measurement noise — how noisy each raw reading is
P0 = 1.0;     // Initial estimate covariance
X0 = 0.0;     // Initial state estimate
```

You also choose what happens to the running filters:
- **Reset filter state** — restart estimation from X₀/P₀ (new initial conditions take effect)
- **Keep current state** — retune smoothly; only Q/R change behavior going forward

### Filter Tuning Guidelines

| Parameter | Low Value | High Value | Impact |
|-----------|-----------|-----------|--------|
| **Q** | Heavier smoothing, slower tracking | Faster tracking, less smoothing | Process noise |
| **R** | Trusts measurements more | Trusts the model more (smoother) | Measurement noise |
| **P₀** | Slow initial convergence | Fast initial convergence | Initial confidence |

## 📈 Sensor Fusion Algorithm

ImuGui combines data from three sensor types:

```
Accelerometer → Gravity direction (roll & pitch reference)
Gyroscope     → Body rotation rates (integrated with measured dt)
Magnetometer  → Tilt-compensated magnetic heading (yaw reference)
       ↓
   Fusion strategy (selectable at runtime)
       ↓
Accurate Orientation (Roll, Pitch, Yaw) — raw AND filtered variants side by side
```

Implemented strategies:
- **Mahony MARG filter** (default) — quaternion-based, gimbal-lock free, PI error correction with anti-windup
- **Complementary filter** — Euler-based, dt-aware blend coefficient α = τ/(τ+dt), wrap-aware yaw
- **Kalman filter** — per-axis two-state [angle, gyro-bias] formulation; learns and removes constant gyro drift online

Both are unit-tested against known orientations, including integration with deliberately irregular sample intervals.

## ⚠️ Current Status

**Status**: ✅ **Stable rebuild — feature-complete**

This codebase is a from-scratch rebuild of the original prototype, with the full feature set reimplemented on a tested, layered architecture. Verified so far:

- ✅ 150 unit tests over all core logic (parsing, filtering, fusion, calibration, serial reconnect, pipeline)
- ✅ Zero-warning build enforced solution-wide; CI on `windows-latest` for every push/PR
- ✅ Clean-clone → `dotnet build` → `dotnet test` → run, with no manual steps
- 🔜 Screenshots/GIF pending the first interactive smoke test on physical hardware

### Future Enhancements
- [ ] Data recording/export (log live streams back to CSV)
- [ ] Session playback controls (pause/scrub replay)
- [ ] Additional fusion strategies (Madgwick, EKF)
- [ ] Quaternion readout display
- [ ] Multi-device support
- [ ] Wireless sensor connectivity (Bluetooth/WiFi)
- [ ] Ellipsoid-fit magnetometer calibration (full cross-axis soft-iron)
- [ ] GUI theme customization

## 🤝 Contributing

Contributions are welcome! To contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/enhancement`)
3. Make your improvements — note that **warnings are errors** and `.editorconfig` style is enforced at build
4. Add or update tests (`tests/ImuGui.Core.Tests`); core logic changes without tests won't pass review
5. Run `dotnet build && dotnet test` locally
6. Commit with descriptive messages and push to your fork
7. Create a Pull Request with a detailed description — CI must be green

### Contribution Ideas

- Add support for additional IMU sensor models / line protocols
- Implement advanced filtering or fusion algorithms
- Improve the 3D visualization (lighting, trails, vectors)
- Add data recording and analysis tools
- Expand documentation
- Optimize chart rendering performance

## 🐛 Troubleshooting

### Common Issues

**CSV file refuses to load**
- The file needs the 10-column contract: `GyroX,…,Temperature` (header optional)
- Numbers must use `.` as the decimal separator (invariant culture); European `0,5`-style files fail loudly by design
- Malformed rows are skipped and counted — check the log for row numbers and reasons

**Serial connection fails**
- The error dialog lists the ports actually present — verify yours appears (use **Refresh**)
- Check the baud rate matches the device (default 115200)
- Ensure USB drivers are installed and no other program holds the port
- DTR is asserted on open; Arduino-style boards reset and need a moment before streaming

**Connected but no data**
- Verify the device emits the [serial line protocol](docs/data-formats.md) (10 comma-separated values per line, `\n`-terminated)
- The first line after opening is discarded on purpose (usually a partial frame)
- Watch the malformed-line count in the log — a wrong field count means a protocol mismatch

**Device unplugged mid-session**
- The status lamp turns orange (**Reconnecting**) and the app retries every 2 s automatically
- Reconnect keeps the session; press **Disconnect** to stop trying

**3D view not rendering**
- Verify OpenGL 3.3+ support (update GPU drivers)
- Remote-desktop sessions sometimes provide software GL below 3.3

**Charts look frozen**
- Check the per-axis X/Y/Z toggles beside each chart
- Confirm frames are flowing (frame counter in the status bar)

**Where are my logs/settings?**
- `%AppData%\ImuGui\` — `settings.json`, `calibration.json`, and `logs/imugui-YYYYMMDD.log`
- A corrupt settings file is quarantined as `settings.json.corrupt` and defaults are used

### Getting Help

- Check [GitHub Issues](https://github.com/74hamed/ImuGui/issues) for similar problems
- Review the rolling log file in `%AppData%\ImuGui\logs`
- Consult [docs/data-formats.md](docs/data-formats.md) for the exact data contracts
- Test with the bundled `samples/imu-sample.csv` to isolate hardware vs. software issues

## 📚 Learning Resources

### IMU Fundamentals
- [Accelerometer Basics](https://learn.sparkfun.com/tutorials/accelerometer-basics)
- [Gyroscope Principles](https://learn.sparkfun.com/tutorials/gyroscope)
- [Magnetometer Theory](https://learn.sparkfun.com/tutorials/magnetometer-basics)

### Sensor Fusion & Filtering
- [Kalman Filter Tutorial](https://www.kalmanfilter.net/)
- [Madgwick/Mahony AHRS report](https://x-io.co.uk/open-source-imu-and-ahrs-algorithms/)
- [Sensor Fusion Concepts](https://en.wikipedia.org/wiki/Sensor_fusion)
- [Complementary Filter Guide](https://www.pieter-jan.com/node/11)

### 3D Graphics & Visualization
- [OpenTK Documentation](https://opentk.net/)
- [OpenGL Programming Guide](https://www.khronos.org/opengl/)
- [ScottPlot Documentation](https://scottplot.net/)

### Hardware Resources
- [MPU6050 (6-axis)](https://invensense.tdk.com/products/motion-tracking/6-axis/mpu6050/)
- [BNO055 (9-axis)](https://www.bosch-sensortec.com/products/smart-sensors/imu/bno055/)
- [LSM9DS1 (9-axis)](https://www.st.com/en/mems-and-sensors/lsm9ds1.html)
- [ICM-20948 (9-axis)](https://www.invensense.com/products/motion-tracking/9-axis/icm-20948/)

## 🔗 Related Projects

- [SharpGL Learning Projects](https://github.com/74hamed/SharpGL-Learning-Projects) - OpenGL fundamentals
- [Sensor3DViewer](https://github.com/74hamed/Sensor3DViewer) - Basic sensor visualization
- [Arduino IMU Projects](https://create.arduino.cc/projecthub/projects?t=IMU)

## 📄 License

This project is licensed under the **[MIT License](LICENSE)** — free to use, modify, and distribute. All NuGet dependencies carry their own permissive licenses (OpenTK: MIT, ScottPlot: MIT, CsvHelper: MS-PL/Apache-2.0, Serilog: Apache-2.0, FluentAssertions 7.x: Apache-2.0).

## 👤 Author

**Hamed** - [GitHub Profile](https://github.com/74hamed)

## 📞 Support

For questions, bug reports, or suggestions:
- 🔗 [GitHub Issues](https://github.com/74hamed/ImuGui/issues)
- 💬 [GitHub Discussions](https://github.com/74hamed/ImuGui/discussions)
- 📧 Contact via GitHub profile

## 🙏 Acknowledgments

- OpenTK team for the OpenGL bindings and GLControl
- ScottPlot team for the charting library
- CsvHelper team for CSV processing
- Serilog contributors for structured logging
- Sebastian Madgwick & Robert Mahony for the published AHRS algorithms
- IMU sensor manufacturers for excellent documentation

---

**Repository**: https://github.com/74hamed/ImuGui
**Last Updated**: July 2026
**Status**: ✅ Stable rebuild
**Version**: 2.0
**Platform**: Windows Desktop (10/11)
**Language**: C# 12 (.NET 8, WinForms)
