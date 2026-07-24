using ImuGui.Core.Models;

namespace ImuGui.Core.Fusion;

/// <summary>
/// A sensor-fusion strategy that estimates attitude (roll, pitch, yaw) from an IMU stream.
/// Callers pass the <em>measured</em> elapsed time between consecutive samples — fusion
/// never assumes a fixed sample rate. Implementations are not thread-safe; callers
/// serialize <see cref="Update"/>.
/// </summary>
public interface IOrientationEstimator
{
    /// <summary>The current attitude as display-ready Euler angles.</summary>
    Orientation CurrentOrientation { get; }

    /// <summary>The current attitude as a body-to-world quaternion.</summary>
    Quaternion CurrentAttitude { get; }

    /// <summary>Advances the estimate with one sample.</summary>
    /// <param name="sample">The (calibrated) sample.</param>
    /// <param name="deltaTime">Measured time since the previous sample; zero for the first.</param>
    void Update(SensorSample sample, TimeSpan deltaTime);

    /// <summary>Discards all state; the next update re-initializes from accelerometer + magnetometer.</summary>
    void Reset();
}
