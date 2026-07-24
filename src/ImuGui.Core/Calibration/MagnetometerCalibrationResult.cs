using ImuGui.Core.Models;

namespace ImuGui.Core.Calibration;

/// <summary>Offsets and scales computed by <see cref="MagnetometerCalibrator"/>.</summary>
/// <param name="HardIronOffset">Constant field offset, subtracted before scaling.</param>
/// <param name="SoftIronScale">Per-axis scale factors equalizing the field ellipsoid's radii.</param>
public sealed record MagnetometerCalibrationResult(Vector3 HardIronOffset, Vector3 SoftIronScale);
