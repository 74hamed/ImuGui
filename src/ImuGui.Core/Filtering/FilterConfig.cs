namespace ImuGui.Core.Filtering;

/// <summary>
/// Tuning parameters for a 1-D scalar Kalman filter, kept separate from the filter's
/// runtime state so parameters can be retuned while a stream is running.
/// </summary>
public sealed record FilterConfig
{
    /// <summary>Process noise Q: how much the true value is assumed to drift between samples.</summary>
    public double ProcessNoise { get; init; } = 0.001;

    /// <summary>Measurement noise R: how noisy each raw measurement is assumed to be.</summary>
    public double MeasurementNoise { get; init; } = 0.1;

    /// <summary>Initial estimate covariance P₀: initial uncertainty of the state.</summary>
    public double InitialCovariance { get; init; } = 1.0;

    /// <summary>Initial state estimate X₀.</summary>
    public double InitialEstimate { get; init; }

    /// <summary>The default tuning applied to every channel until the user retunes.</summary>
    public static FilterConfig Default { get; } = new();

    /// <summary>Throws when the parameters are out of range (Q &lt; 0, R ≤ 0, P₀ &lt; 0, or non-finite).</summary>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is invalid.</exception>
    public void Validate()
    {
        ThrowIfNotFinite(ProcessNoise, nameof(ProcessNoise));
        ThrowIfNotFinite(MeasurementNoise, nameof(MeasurementNoise));
        ThrowIfNotFinite(InitialCovariance, nameof(InitialCovariance));
        ThrowIfNotFinite(InitialEstimate, nameof(InitialEstimate));

        if (ProcessNoise < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProcessNoise), ProcessNoise, "Process noise Q must be ≥ 0.");
        }

        if (MeasurementNoise <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MeasurementNoise), MeasurementNoise, "Measurement noise R must be > 0.");
        }

        if (InitialCovariance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialCovariance), InitialCovariance, "Initial covariance P₀ must be ≥ 0.");
        }
    }

    private static void ThrowIfNotFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "Filter parameters must be finite numbers.");
        }
    }
}
