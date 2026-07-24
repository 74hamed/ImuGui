namespace ImuGui.Core.Fusion;

/// <summary>Angle conversion and wrapping helpers shared by fusion, calibration, and display code.</summary>
public static class AngleMath
{
    private const double DegreesPerRadian = 180.0 / Math.PI;

    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    public static double DegreesToRadians(double degrees) => degrees / DegreesPerRadian;

    /// <summary>Converts radians to degrees.</summary>
    /// <param name="radians">The angle in radians.</param>
    public static double RadiansToDegrees(double radians) => radians * DegreesPerRadian;

    /// <summary>Wraps an angle in degrees to [0, 360).</summary>
    /// <param name="degrees">The angle in degrees.</param>
    public static double WrapDegreesPositive(double degrees)
    {
        double wrapped = degrees % 360.0;
        return wrapped < 0 ? wrapped + 360.0 : wrapped;
    }

    /// <summary>Wraps an angle in degrees to (-180, 180].</summary>
    /// <param name="degrees">The angle in degrees.</param>
    public static double WrapDegreesSigned(double degrees)
    {
        double wrapped = WrapDegreesPositive(degrees);
        return wrapped > 180.0 ? wrapped - 360.0 : wrapped;
    }

    /// <summary>Wraps an angle in radians to (-π, π].</summary>
    /// <param name="radians">The angle in radians.</param>
    public static double WrapRadiansSigned(double radians)
    {
        double wrapped = radians % (2.0 * Math.PI);
        if (wrapped <= -Math.PI)
        {
            wrapped += 2.0 * Math.PI;
        }
        else if (wrapped > Math.PI)
        {
            wrapped -= 2.0 * Math.PI;
        }

        return wrapped;
    }

    /// <summary>
    /// Returns the signed shortest rotation, in degrees, that takes <paramref name="fromDegrees"/>
    /// to <paramref name="toDegrees"/> (wrap-aware; e.g. from 359° to 1° returns +2°).
    /// </summary>
    /// <param name="fromDegrees">The starting angle in degrees.</param>
    /// <param name="toDegrees">The target angle in degrees.</param>
    public static double ShortestDifferenceDegrees(double fromDegrees, double toDegrees) =>
        WrapDegreesSigned(toDegrees - fromDegrees);
}
