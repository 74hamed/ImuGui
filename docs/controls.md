# Controls Reference

## Environment view (3-D camera)

The mapping below is implemented in `EnvironmentGlView` and verified against
`ImuGui.Core.Cameras.OrbitCamera`'s unit tests — the on-screen hint bar shows the same
bindings.

The bindings follow **Blender's viewport navigation**; the modifier held when the middle
button goes down selects the drag mode, exactly as Blender does.

| Input | Action |
|---|---|
| **Middle-drag** | Orbit around the rotation center (horizontal = yaw, vertical = pitch, 0.01 rad/px; pitch clamped to ±89°) |
| **Shift + middle-drag** | Pan — moves the rotation center in the view plane (scaled by distance) |
| **Ctrl + middle-drag** | Zoom — drag up = closer (1% per pixel) |
| **Mouse wheel** | Zoom (wheel up = closer; distance clamped to [1, 60]) |
| **Home** (or **R**) | Reset camera to the default pose (45° azimuth, 30° elevation, distance 8, center at origin) |
| **Reset camera button** | Same as **Home** |

The **Show grid** checkbox toggles the ground reference grid.

## 3-D cube views

Each of the two cube views selects one quantity; a quantity used by one view is removed
from the other's list. Each view also has its own **Filtered** toggle, independent of the
global one.

| Quantity | Cube rotation |
|---|---|
| Orientation | The fused attitude (roll/pitch/yaw) |
| Gyroscope | Channel values as Euler angles, 1 °/s ≙ 1° |
| Accelerometer | Channel values as Euler angles, 1 g ≙ 90° |
| Magnetometer | Channel values as Euler angles, 1 unit ≙ 2° |

## Main window

- **Connect / Disconnect** starts and stops the selected source. The status lamp shows
  the *real* connection state: gray disconnected, orange connecting/reconnecting, green
  connected, red faulted.
- **Use filtered data** is the global raw/filtered toggle: it switches the readouts,
  charts, instruments, and the environment view together. Charts can additionally
  **overlay the raw signal** for comparison.
- **Tune filters…** edits the Kalman parameters (Q/R/P₀/X₀) for all channels, with a
  choice of resetting or preserving the filters' runtime state.
- **Fusion** selects the estimator: Mahony (quaternion, default, no gimbal lock),
  complementary (Euler), or Kalman (Euler, with an online gyro-bias state).
- **Calibrate…** opens the calibration workflow (gyro bias → six-position accelerometer
  → magnetometer figure-eight); **Apply calibration** toggles the active profile on the
  live stream.
- Charts scroll over a configurable time window and retain a **bounded** history
  (4096 points per chart) — old points are dropped.

All preferences (source, port/baud, filter tuning, chart prefs, cube selections, grid,
calibration profile) persist in `%AppData%\ImuGui\`.
