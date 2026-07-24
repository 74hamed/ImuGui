using ImuGui.Core.Models;

namespace ImuGui.Core.Fusion;

/// <summary>
/// Attitude-from-vector helpers: roll/pitch from the accelerometer's gravity direction and
/// the tilt-compensated magnetometer heading. All formulas assume the ImuGui NED conventions
/// (body X forward, Y right, Z down; level specific force = (0, 0, -1) g).
/// </summary>
public static class TiltCompensation
{
    /// <summary>Roll angle in radians implied by a quasi-static accelerometer reading.</summary>
    /// <param name="accelerometer">The specific-force vector in g.</param>
    public static double ComputeRollRadians(Vector3 accelerometer) =>
        Math.Atan2(-accelerometer.Y, -accelerometer.Z);

    /// <summary>Pitch angle in radians implied by a quasi-static accelerometer reading.</summary>
    /// <param name="accelerometer">The specific-force vector in g.</param>
    public static double ComputePitchRadians(Vector3 accelerometer) =>
        Math.Atan2(
            accelerometer.X,
            Math.Sqrt((accelerometer.Y * accelerometer.Y) + (accelerometer.Z * accelerometer.Z)));

    /// <summary>
    /// Compass heading from a magnetometer reading, de-rotated by the current roll and pitch.
    /// Returns radians in (-π, π]; 0 = magnetic north, positive clockwise (east = +π/2).
    /// </summary>
    /// <param name="magnetometer">The magnetic field vector in body frame.</param>
    /// <param name="rollRadians">Current roll in radians.</param>
    /// <param name="pitchRadians">Current pitch in radians.</param>
    public static double ComputeHeadingRadians(Vector3 magnetometer, double rollRadians, double pitchRadians)
    {
        double sinRoll = Math.Sin(rollRadians);
        double cosRoll = Math.Cos(rollRadians);
        double sinPitch = Math.Sin(pitchRadians);
        double cosPitch = Math.Cos(pitchRadians);

        // De-rotate the field into the horizontal plane: m_h = Ry(pitch) · Rx(roll) · m.
        double horizontalX = (magnetometer.X * cosPitch)
            + (magnetometer.Y * sinRoll * sinPitch)
            + (magnetometer.Z * cosRoll * sinPitch);
        double horizontalY = (magnetometer.Y * cosRoll) - (magnetometer.Z * sinRoll);

        return Math.Atan2(-horizontalY, horizontalX);
    }

    /// <summary>Convenience wrapper for <see cref="ComputeHeadingRadians"/> returning degrees in [0, 360).</summary>
    /// <param name="magnetometer">The magnetic field vector in body frame.</param>
    /// <param name="rollRadians">Current roll in radians.</param>
    /// <param name="pitchRadians">Current pitch in radians.</param>
    public static double ComputeHeadingDegrees(Vector3 magnetometer, double rollRadians, double pitchRadians) =>
        AngleMath.WrapDegreesPositive(
            AngleMath.RadiansToDegrees(ComputeHeadingRadians(magnetometer, rollRadians, pitchRadians)));
}
