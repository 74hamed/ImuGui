namespace ImuGui.Core.Fusion;

/// <summary>Gains for <see cref="MahonyOrientationEstimator"/>.</summary>
public sealed record MahonyOptions
{
    /// <summary>Proportional gain Kp: how strongly accel/mag corrections pull the attitude.</summary>
    public double ProportionalGain { get; init; } = 1.0;

    /// <summary>Integral gain Ki: compensates slow gyroscope bias; 0 disables the integral term.</summary>
    public double IntegralGain { get; init; } = 0.05;

    /// <summary>Throws when a gain is negative or non-finite.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A gain is invalid.</exception>
    public void Validate()
    {
        if (!double.IsFinite(ProportionalGain) || ProportionalGain < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProportionalGain), ProportionalGain, "Proportional gain must be a finite value ≥ 0.");
        }

        if (!double.IsFinite(IntegralGain) || IntegralGain < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IntegralGain), IntegralGain, "Integral gain must be a finite value ≥ 0.");
        }
    }
}
