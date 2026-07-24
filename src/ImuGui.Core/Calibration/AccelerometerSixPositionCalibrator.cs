using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>
/// Classic six-position accelerometer calibration: the device rests on each face while
/// samples are captured; each axis then has a "down" mean (≈ −1 g) and an "up" mean
/// (≈ +1 g), from which per-axis bias = (up + down) / 2 and scale = 2 / (up − down).
/// </summary>
public sealed class AccelerometerSixPositionCalibrator
{
    /// <summary>The minimum samples per face required.</summary>
    public const int MinimumSamplesPerFace = 10;

    private const double MinimumAxisSpanG = 0.5;

    private readonly Dictionary<AccelerometerCalibrationFace, Accumulator> _accumulators = new();

    /// <summary>The faces captured so far (with enough samples).</summary>
    public IReadOnlyCollection<AccelerometerCalibrationFace> CapturedFaces =>
        _accumulators.Where(pair => pair.Value.Count >= MinimumSamplesPerFace)
            .Select(pair => pair.Key)
            .ToArray();

    /// <summary>Adds one rest sample for the given face.</summary>
    /// <param name="face">The face the device currently rests on.</param>
    /// <param name="sample">The sample.</param>
    public void AddSample(AccelerometerCalibrationFace face, SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        Accumulator accumulator = _accumulators.TryGetValue(face, out Accumulator existing)
            ? existing
            : default;
        _accumulators[face] = new Accumulator(accumulator.Sum + sample.Accelerometer, accumulator.Count + 1);
    }

    /// <summary>How many samples have been captured for a face.</summary>
    /// <param name="face">The face.</param>
    public int SampleCountFor(AccelerometerCalibrationFace face) =>
        _accumulators.TryGetValue(face, out Accumulator accumulator) ? accumulator.Count : 0;

    /// <summary>True when the face has enough samples.</summary>
    /// <param name="face">The face.</param>
    public bool IsFaceCaptured(AccelerometerCalibrationFace face) =>
        SampleCountFor(face) >= MinimumSamplesPerFace;

    /// <summary>Computes bias and scale from all six captured faces.</summary>
    /// <exception cref="CalibrationException">Faces are missing or the readings are implausible.</exception>
    public AccelerometerCalibrationResult ComputeResult()
    {
        AccelerometerCalibrationFace[] missing = Enum.GetValues<AccelerometerCalibrationFace>()
            .Where(face => !IsFaceCaptured(face))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new CalibrationException(
                "Accelerometer calibration is missing positions: "
                + $"{string.Join(", ", missing)} (need ≥ {MinimumSamplesPerFace} samples each).");
        }

        double biasX = ComputeAxis(
            Mean(AccelerometerCalibrationFace.XUp).X, Mean(AccelerometerCalibrationFace.XDown).X, "X", out double scaleX);
        double biasY = ComputeAxis(
            Mean(AccelerometerCalibrationFace.YUp).Y, Mean(AccelerometerCalibrationFace.YDown).Y, "Y", out double scaleY);
        double biasZ = ComputeAxis(
            Mean(AccelerometerCalibrationFace.ZUp).Z, Mean(AccelerometerCalibrationFace.ZDown).Z, "Z", out double scaleZ);

        return new AccelerometerCalibrationResult(
            new Vector3(biasX, biasY, biasZ), new Vector3(scaleX, scaleY, scaleZ));
    }

    /// <summary>Discards all captured samples.</summary>
    public void Reset() => _accumulators.Clear();

    private static double ComputeAxis(double upMean, double downMean, string axisName, out double scale)
    {
        double span = upMean - downMean;
        if (span < MinimumAxisSpanG)
        {
            throw new CalibrationException(
                $"Accelerometer {axisName}-axis readings are implausible (up {upMean:F3} g vs down "
                + $"{downMean:F3} g). Make sure the device actually rested on each face.");
        }

        scale = 2.0 / span;
        return (upMean + downMean) / 2.0;
    }

    private Vector3 Mean(AccelerometerCalibrationFace face)
    {
        Accumulator accumulator = _accumulators[face];
        return accumulator.Sum / accumulator.Count;
    }

    private readonly record struct Accumulator(Vector3 Sum, int Count);
}
