# Architecture Notes

## Layering

```
ImuGui.App          WinForms shell: Program (DI bootstrap), MainForm + panels (passive views),
                    MainPresenter (all UI-side logic), dialogs, JSON settings stores
   │ depends on
ImuGui.Rendering    OpenTK 3-D views (CubeGlView, EnvironmentGlView) — the ONLY project
                    referencing OpenGL; raw GL calls live behind IRenderer
ImuGui.Instruments  Owner-drawn artificial horizon & heading indicator (GDI+, zero deps)
   │
ImuGui.Core         net8.0, no UI references. Sources, filtering, fusion, calibration,
                    pipeline, ring buffer, orbit-camera math. Fully unit-tested.
```

## Data flow & threading

```
acquisition thread                          UI thread
──────────────────                          ─────────
ISensorSource ──SampleReceived──▶ SensorPipeline
   (CSV replay / serial)             │ calibrate → FilterBank → two estimators
                                     │ publishes immutable ProcessedFrame
                                     ├── LatestFrame (volatile snapshot) ◀── render timer (~30 FPS)
                                     └── FrameProcessed event ──▶ chart ring buffers (bounded, locked)
```

- **Acquisition never runs on a UI timer.** Sources read on background threads (serial
  uses a dedicated long-running thread); the WinForms timer is a *render* cadence that
  only reads the latest frame. Data rate and render rate are fully decoupled.
- **dt is measured**, never assumed: the pipeline computes it from consecutive sample
  timestamps, which come from the injectable `IClock` (serial) or the replay schedule
  (CSV). Fusion receives dt per update; tests drive it with irregular intervals.
- **Raw vs. filtered:** every frame carries both variants (and both fused orientations —
  two estimator instances run side by side), so the global toggle and the per-view
  toggles are pure view concerns with zero pipeline coupling.
- **Bounded memory:** chart history lives in fixed-capacity `RingBuffer<T>`s; adding
  past capacity drops the oldest point.

## Fusion conventions

Documented in [data-formats.md](data-formats.md). The Mahony estimator integrates gyro
rates on a quaternion with PI correction from the accelerometer's gravity direction and
the magnetometer's field direction (with anti-windup on the integral term); the first
sample initializes attitude directly from accel + tilt-compensated heading. The
complementary estimator propagates Euler angles through proper Euler-rate kinematics and
blends with a dt-aware coefficient α = τ/(τ+dt), wrap-aware for yaw. The Kalman estimator
runs the classic two-state [angle, gyro-bias] filter per axis over the same kinematics —
its bias state learns and removes constant gyro drift online.

## Testing strategy

Everything time-dependent takes `IClock`; tests use a virtual-time `FakeClock`, so replay
pacing and reconnect delays run instantly and deterministically. Serial hardware is
faked behind `ISerialPortConnection(Factory)` with scripted reads (lines, timeouts,
IO failures) to cover reconnect, fault, and malformed-line paths. Fusion is validated
against synthetic samples generated from known attitudes (the generator and the
estimators share the documented conventions, so a sign error fails loudly). The bundled
sample CSV doubles as an integration fixture.
