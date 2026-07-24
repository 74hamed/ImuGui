namespace ImuGui.Core.Models;

/// <summary>The connection state of a sensor source.</summary>
public enum SensorConnectionState
{
    /// <summary>The source is not running.</summary>
    Disconnected,

    /// <summary>The source is starting up (opening a port, loading a file).</summary>
    Connecting,

    /// <summary>The source is running and delivering samples.</summary>
    Connected,

    /// <summary>The source lost its device and is attempting to reconnect.</summary>
    Reconnecting,

    /// <summary>The source stopped after an unrecoverable error.</summary>
    Faulted,
}
