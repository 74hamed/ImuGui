namespace ImuGui.Core.Sources;

/// <summary>
/// Thrown when a sensor source cannot start or irrecoverably fails. The message is
/// written to be shown to the user as-is (actionable, no stack traces).
/// </summary>
public sealed class SensorSourceException : Exception
{
    /// <summary>Creates the exception.</summary>
    public SensorSourceException()
    {
    }

    /// <summary>Creates the exception with a user-presentable message.</summary>
    /// <param name="message">The user-presentable message.</param>
    public SensorSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a user-presentable message and inner cause.</summary>
    /// <param name="message">The user-presentable message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public SensorSourceException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
