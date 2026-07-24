namespace ImuGui.Core.Sources;

/// <summary>
/// A minimal seam over one open serial connection so <see cref="SerialSensorSource"/>
/// can be tested against fakes.
/// </summary>
public interface ISerialPortConnection : IDisposable
{
    /// <summary>Whether the underlying port is open.</summary>
    bool IsOpen { get; }

    /// <summary>Opens the port. Throws on failure (port missing, busy, access denied).</summary>
    void Open();

    /// <summary>
    /// Reads one newline-terminated line, blocking up to the configured read timeout.
    /// Throws <see cref="TimeoutException"/> when no line arrived in time, and
    /// <see cref="IOException"/>/<see cref="InvalidOperationException"/> when the
    /// connection is lost.
    /// </summary>
    string ReadLine();

    /// <summary>Closes the port. Safe to call when already closed.</summary>
    void Close();
}
