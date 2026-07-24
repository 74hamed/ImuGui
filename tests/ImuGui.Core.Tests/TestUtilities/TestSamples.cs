using ImuGui.Core.Fusion;
using ImuGui.Core.Models;

namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>Builders for physically consistent synthetic samples (ImuGui NED conventions).</summary>
internal static class TestSamples
{
    /// <summary>World magnetic field: unit magnitude, 60° inclination, horizontal component due north.</summary>
    internal static readonly Vector3 MagneticFieldWorld = new(
        Math.Cos(AngleMath.DegreesToRadians(60)), 0, Math.Sin(AngleMath.DegreesToRadians(60)));

    /// <summary>A level, stationary, north-facing sample.</summary>
    internal static SensorSample Level(TimeSpan? timestamp = null) =>
        FromAttitude(0, 0, 0, timestamp);

    /// <summary>
    /// A stationary sample as measured by a device at the given attitude: the accelerometer
    /// reads R(q)ᵀ·(0,0,−1) g, the magnetometer reads R(q)ᵀ·field, and the gyro is zero.
    /// </summary>
    internal static SensorSample FromAttitude(
        double rollRadians, double pitchRadians, double yawRadians, TimeSpan? timestamp = null)
    {
        Quaternion attitude = Quaternion.FromEulerAngles(rollRadians, pitchRadians, yawRadians);
        return new SensorSample(
            timestamp ?? TimeSpan.Zero,
            Vector3.Zero,
            attitude.InverseRotate(new Vector3(0, 0, -1)),
            attitude.InverseRotate(MagneticFieldWorld),
            25.0);
    }

    /// <summary>A sample with only gyroscope rates (accel/mag zero so fusion skips corrections).</summary>
    internal static SensorSample GyroOnly(
        double xDegPerSec, double yDegPerSec, double zDegPerSec, TimeSpan? timestamp = null) =>
        new(
            timestamp ?? TimeSpan.Zero,
            new Vector3(xDegPerSec, yDegPerSec, zDegPerSec),
            Vector3.Zero,
            Vector3.Zero,
            25.0);

    /// <summary>A sample with explicit vectors and default temperature.</summary>
    internal static SensorSample With(
        Vector3? gyro = null, Vector3? accel = null, Vector3? mag = null, TimeSpan? timestamp = null) =>
        new(
            timestamp ?? TimeSpan.Zero,
            gyro ?? Vector3.Zero,
            accel ?? new Vector3(0, 0, -1),
            mag ?? TestSamples.MagneticFieldWorld,
            25.0);
}
