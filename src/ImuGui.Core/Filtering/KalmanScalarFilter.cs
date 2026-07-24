namespace ImuGui.Core.Filtering;

/// <summary>
/// A 1-D Kalman filter with a random-walk state model (state transition = identity):
/// predict inflates the covariance by Q, update blends the measurement by the Kalman gain
/// K = P / (P + R). Small Q relative to R yields heavy smoothing; large Q tracks quickly.
/// </summary>
public sealed class KalmanScalarFilter : IScalarFilter
{
    private double _estimate;
    private double _covariance;

    /// <summary>Creates the filter from validated tuning parameters.</summary>
    /// <param name="config">The tuning parameters.</param>
    public KalmanScalarFilter(FilterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        Config = config;
        _estimate = config.InitialEstimate;
        _covariance = config.InitialCovariance;
    }

    /// <inheritdoc />
    public FilterConfig Config { get; private set; }

    /// <inheritdoc />
    public double Value => _estimate;

    /// <inheritdoc />
    public double Update(double measurement)
    {
        // Predict: the state is modeled as a random walk, so only uncertainty grows.
        _covariance += Config.ProcessNoise;

        // Update: blend the measurement in proportion to relative uncertainty.
        double gain = _covariance / (_covariance + Config.MeasurementNoise);
        _estimate += gain * (measurement - _estimate);
        _covariance *= 1 - gain;

        return _estimate;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _estimate = Config.InitialEstimate;
        _covariance = Config.InitialCovariance;
    }

    /// <inheritdoc />
    public void Retune(FilterConfig config, RetuneBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        Config = config;
        if (behavior == RetuneBehavior.ResetState)
        {
            Reset();
        }
    }
}
