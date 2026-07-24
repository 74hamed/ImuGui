using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>Bias and per-axis scale computed by <see cref="AccelerometerSixPositionCalibrator"/>.</summary>
/// <param name="Bias">Offset in g, subtracted before scaling.</param>
/// <param name="Scale">Per-axis scale factors mapping the corrected span to ±1 g.</param>
public sealed record AccelerometerCalibrationResult(Vector3 Bias, Vector3 Scale);
