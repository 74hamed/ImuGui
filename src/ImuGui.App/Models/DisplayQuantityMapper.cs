using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using Orientation = ImuGui.Core.Models.Orientation;

namespace ImuGui.App.Models;

/// <summary>Maps a processed frame + display quantity to the attitude a cube view renders.</summary>
internal static class DisplayQuantityMapper
{
    // Display gains turning vector readings into visible rotation angles.
    private const double AccelerometerDegreesPerG = 90;
    private const double MagnetometerDegreesPerUnit = 2;
    private const double GyroscopeDegreesPerDegPerSec = 1;

    /// <summary>Computes the cube attitude for one view.</summary>
    /// <param name="frame">The current frame.</param>
    /// <param name="quantity">The selected quantity.</param>
    /// <param name="useFiltered">The view's raw/filtered toggle.</param>
    internal static Quaternion AttitudeFor(ProcessedFrame frame, DisplayQuantity quantity, bool useFiltered)
    {
        SensorSample sample = useFiltered ? frame.FilteredSample : frame.RawSample;
        return quantity switch
        {
            DisplayQuantity.Accelerometer => FromVector(sample.Accelerometer, AccelerometerDegreesPerG),
            DisplayQuantity.Magnetometer => FromVector(sample.Magnetometer, MagnetometerDegreesPerUnit),
            DisplayQuantity.Gyroscope => FromVector(sample.Gyroscope, GyroscopeDegreesPerDegPerSec),
            DisplayQuantity.Orientation => FromOrientation(
                useFiltered ? frame.FilteredOrientation : frame.RawOrientation),
            _ => Quaternion.Identity,
        };
    }

    private static Quaternion FromOrientation(Orientation orientation) => Quaternion.FromEulerAngles(
        AngleMath.DegreesToRadians(orientation.RollDegrees),
        AngleMath.DegreesToRadians(orientation.PitchDegrees),
        AngleMath.DegreesToRadians(orientation.YawDegrees));

    private static Quaternion FromVector(Vector3 vector, double degreesPerUnit) =>
        Quaternion.FromEulerAngles(
            AngleMath.DegreesToRadians(vector.X * degreesPerUnit),
            AngleMath.DegreesToRadians(vector.Y * degreesPerUnit),
            AngleMath.DegreesToRadians(vector.Z * degreesPerUnit));
}
