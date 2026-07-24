using System.Globalization;

namespace ImuGui.Core.Models;

/// <summary>
/// A display-ready attitude expressed as Euler angles in degrees (ZYX / yaw-pitch-roll,
/// NED conventions: positive roll right-side down, positive pitch nose up, yaw as a
/// compass heading increasing clockwise from north).
/// </summary>
/// <param name="RollDegrees">Roll about body X in degrees, normally in (-180, 180].</param>
/// <param name="PitchDegrees">Pitch about body Y in degrees, normally in [-90, 90].</param>
/// <param name="YawDegrees">Yaw/heading in degrees, normally in [0, 360).</param>
public readonly record struct Orientation(double RollDegrees, double PitchDegrees, double YawDegrees)
{
    /// <summary>The level, north-facing orientation.</summary>
    public static Orientation Zero { get; } = new(0, 0, 0);

    /// <summary>Formats the orientation with invariant culture.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"roll {RollDegrees:F1}°, pitch {PitchDegrees:F1}°, yaw {YawDegrees:F1}°");
}
