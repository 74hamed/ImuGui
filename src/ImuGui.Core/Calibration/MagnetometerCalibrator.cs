using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>
/// Min/max magnetometer calibration: while the user sweeps the device through all
/// orientations (figure-eight), per-axis extremes are tracked. Hard-iron offset is the
/// per-axis midpoint; soft-iron correction is the simplified axis-aligned ellipsoid model
/// (per-axis radii equalized to their mean — no cross-axis terms).
/// </summary>
public sealed class MagnetometerCalibrator
{
    /// <summary>The minimum number of samples required.</summary>
    public const int MinimumSampleCount = 100;

    private const double MinimumRelativeAxisRange = 0.2;

    private Vector3 _minimum = new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
    private Vector3 _maximum = new(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

    /// <summary>How many samples have been captured.</summary>
    public int SampleCount { get; private set; }

    /// <summary>Per-axis minimum seen so far (for live coverage display).</summary>
    public Vector3 CurrentMinimum => _minimum;

    /// <summary>Per-axis maximum seen so far (for live coverage display).</summary>
    public Vector3 CurrentMaximum => _maximum;

    /// <summary>Adds one sample captured while sweeping the device.</summary>
    /// <param name="sample">The sample.</param>
    public void AddSample(SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        Vector3 magnetometer = sample.Magnetometer;
        _minimum = new Vector3(
            Math.Min(_minimum.X, magnetometer.X),
            Math.Min(_minimum.Y, magnetometer.Y),
            Math.Min(_minimum.Z, magnetometer.Z));
        _maximum = new Vector3(
            Math.Max(_maximum.X, magnetometer.X),
            Math.Max(_maximum.Y, magnetometer.Y),
            Math.Max(_maximum.Z, magnetometer.Z));
        SampleCount++;
    }

    /// <summary>Computes hard-iron offset and soft-iron scale from the captured extremes.</summary>
    /// <exception cref="CalibrationException">Coverage is insufficient to calibrate.</exception>
    public MagnetometerCalibrationResult ComputeResult()
    {
        if (SampleCount < MinimumSampleCount)
        {
            throw new CalibrationException(
                $"Magnetometer calibration needs at least {MinimumSampleCount} samples; only "
                + $"{SampleCount} were captured. Keep sweeping the device in a figure-eight.");
        }

        var radii = new Vector3(
            (_maximum.X - _minimum.X) / 2.0,
            (_maximum.Y - _minimum.Y) / 2.0,
            (_maximum.Z - _minimum.Z) / 2.0);

        double largestRadius = Math.Max(radii.X, Math.Max(radii.Y, radii.Z));
        if (largestRadius <= 0
            || radii.X < largestRadius * MinimumRelativeAxisRange
            || radii.Y < largestRadius * MinimumRelativeAxisRange
            || radii.Z < largestRadius * MinimumRelativeAxisRange)
        {
            throw new CalibrationException(
                "Magnetometer coverage is too flat — at least one axis barely changed. Rotate the "
                + "device through all orientations (a slow 3-D figure-eight) and try again.");
        }

        var offset = new Vector3(
            (_maximum.X + _minimum.X) / 2.0,
            (_maximum.Y + _minimum.Y) / 2.0,
            (_maximum.Z + _minimum.Z) / 2.0);

        double meanRadius = (radii.X + radii.Y + radii.Z) / 3.0;
        var scale = new Vector3(meanRadius / radii.X, meanRadius / radii.Y, meanRadius / radii.Z);

        return new MagnetometerCalibrationResult(offset, scale);
    }

    /// <summary>Discards all captured samples.</summary>
    public void Reset()
    {
        _minimum = new Vector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        _maximum = new Vector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        SampleCount = 0;
    }
}
