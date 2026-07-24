namespace ImuGui.Core.Calibration;

/// <summary>
/// Thrown when a calibration routine cannot produce a result. Messages are written to be
/// shown to the user as-is (e.g. "rotate the device through more orientations").
/// </summary>
public sealed class CalibrationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public CalibrationException()
    {
    }

    /// <summary>Creates the exception with a user-presentable message.</summary>
    /// <param name="message">The user-presentable message.</param>
    public CalibrationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a user-presentable message and inner cause.</summary>
    /// <param name="message">The user-presentable message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public CalibrationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
