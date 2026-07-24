namespace ImuGui.Core.Fusion;

/// <summary>Tuning for <see cref="ComplementaryOrientationEstimator"/>.</summary>
public sealed record ComplementaryOptions
{
    /// <summary>
    /// Blend time constant τ in seconds. Each update blends gyro prediction and
    /// accel/mag measurement with α = τ / (τ + dt): larger τ trusts the gyro longer.
    /// </summary>
    public double TimeConstantSeconds { get; init; } = 1.0;

    /// <summary>Throws when the time constant is not a positive finite value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The time constant is invalid.</exception>
    public void Validate()
    {
        if (!double.IsFinite(TimeConstantSeconds) || TimeConstantSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TimeConstantSeconds), TimeConstantSeconds, "Time constant must be a finite value > 0.");
        }
    }
}
