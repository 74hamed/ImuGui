using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>
/// Estimates gyroscope zero-rate bias as the mean rate over a stationary capture.
/// Feed samples while the device rests untouched, then compute.
/// </summary>
public sealed class GyroscopeBiasCalibrator
{
    /// <summary>The minimum number of stationary samples required.</summary>
    public const int MinimumSampleCount = 20;

    private Vector3 _sum = Vector3.Zero;

    /// <summary>How many samples have been captured.</summary>
    public int SampleCount { get; private set; }

    /// <summary>True once enough samples have been captured to compute.</summary>
    public bool HasEnoughSamples => SampleCount >= MinimumSampleCount;

    /// <summary>Adds one stationary sample.</summary>
    /// <param name="sample">The sample.</param>
    public void AddSample(SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        _sum += sample.Gyroscope;
        SampleCount++;
    }

    /// <summary>Computes the bias (mean angular rate) in deg/s.</summary>
    /// <exception cref="CalibrationException">Not enough samples were captured.</exception>
    public Vector3 ComputeBias()
    {
        if (!HasEnoughSamples)
        {
            throw new CalibrationException(
                $"Gyroscope calibration needs at least {MinimumSampleCount} stationary samples; "
                + $"only {SampleCount} were captured. Keep the device still and capture longer.");
        }

        return _sum / SampleCount;
    }

    /// <summary>Discards all captured samples.</summary>
    public void Reset()
    {
        _sum = Vector3.Zero;
        SampleCount = 0;
    }
}
