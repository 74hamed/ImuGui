using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>
/// Immutable per-device correction parameters. Applied to each raw sample as:
/// gyro − bias; (accel − bias) ⊙ scale; (mag − hardIron) ⊙ softIronScale.
/// </summary>
public sealed record CalibrationProfile
{
    /// <summary>Gyroscope zero-rate bias in deg/s, subtracted from every sample.</summary>
    public Vector3 GyroscopeBias { get; init; } = Vector3.Zero;

    /// <summary>Accelerometer offset in g, subtracted before scaling.</summary>
    public Vector3 AccelerometerBias { get; init; } = Vector3.Zero;

    /// <summary>Per-axis accelerometer scale factors.</summary>
    public Vector3 AccelerometerScale { get; init; } = new(1, 1, 1);

    /// <summary>Magnetometer hard-iron offset, subtracted before scaling.</summary>
    public Vector3 MagnetometerHardIronOffset { get; init; } = Vector3.Zero;

    /// <summary>Per-axis magnetometer soft-iron scale factors (axis-aligned ellipsoid model).</summary>
    public Vector3 MagnetometerSoftIronScale { get; init; } = new(1, 1, 1);

    /// <summary>When the profile was created (UTC), for display purposes.</summary>
    public DateTimeOffset? CreatedUtc { get; init; }

    /// <summary>The no-op profile.</summary>
    public static CalibrationProfile Identity { get; } = new();

    /// <summary>True when applying the profile leaves samples unchanged.</summary>
    public bool IsIdentity =>
        GyroscopeBias == Vector3.Zero
        && AccelerometerBias == Vector3.Zero
        && AccelerometerScale == new Vector3(1, 1, 1)
        && MagnetometerHardIronOffset == Vector3.Zero
        && MagnetometerSoftIronScale == new Vector3(1, 1, 1);

    /// <summary>Returns a corrected copy of the sample.</summary>
    /// <param name="sample">The raw sample.</param>
    public SensorSample Apply(SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return sample with
        {
            Gyroscope = sample.Gyroscope - GyroscopeBias,
            Accelerometer = (sample.Accelerometer - AccelerometerBias).Scale(AccelerometerScale),
            Magnetometer = (sample.Magnetometer - MagnetometerHardIronOffset).Scale(MagnetometerSoftIronScale),
        };
    }
}
