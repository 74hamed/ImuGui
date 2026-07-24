namespace ImuGui.Core.Fusion;

/// <summary>Tuning for <see cref="KalmanOrientationEstimator"/> (all angles in radians).</summary>
public sealed record KalmanEstimatorOptions
{
    /// <summary>Process noise of the angle state Q_angle (rad²·s⁻¹ scale).</summary>
    public double AngleProcessNoise { get; init; } = 0.001;

    /// <summary>Process noise of the gyro-bias state Q_bias; larger adapts to drift faster.</summary>
    public double BiasProcessNoise { get; init; } = 0.003;

    /// <summary>Measurement noise R of the accel/mag angle observations (rad²).</summary>
    public double MeasurementNoise { get; init; } = 0.03;

    /// <summary>Throws when a parameter is non-finite or out of range (Q ≥ 0, R &gt; 0).</summary>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is invalid.</exception>
    public void Validate()
    {
        if (!double.IsFinite(AngleProcessNoise) || AngleProcessNoise < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AngleProcessNoise), AngleProcessNoise, "Angle process noise must be a finite value ≥ 0.");
        }

        if (!double.IsFinite(BiasProcessNoise) || BiasProcessNoise < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BiasProcessNoise), BiasProcessNoise, "Bias process noise must be a finite value ≥ 0.");
        }

        if (!double.IsFinite(MeasurementNoise) || MeasurementNoise <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MeasurementNoise), MeasurementNoise, "Measurement noise must be a finite value > 0.");
        }
    }
}
