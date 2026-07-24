namespace ImuGui.Core.Sources;

/// <summary>Describes a recoverable runtime error inside a sensor source.</summary>
public sealed class SensorSourceErrorEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    /// <param name="message">A user-presentable description of the error.</param>
    /// <param name="exception">The underlying exception, when one exists.</param>
    public SensorSourceErrorEventArgs(string message, Exception? exception = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
    }

    /// <summary>A user-presentable description of the error.</summary>
    public string Message { get; }

    /// <summary>The underlying exception, when one exists.</summary>
    public Exception? Exception { get; }
}
