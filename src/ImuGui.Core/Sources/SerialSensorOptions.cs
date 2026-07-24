using System.IO.Ports;

namespace ImuGui.Core.Sources;

/// <summary>Configuration for <see cref="SerialSensorSource"/>. Framing defaults to 8N1.</summary>
public sealed record SerialSensorOptions
{
    /// <summary>The port name, e.g. "COM3".</summary>
    public required string PortName { get; init; }

    /// <summary>Baud rate; 115200 by default.</summary>
    public int BaudRate { get; init; } = 115200;

    /// <summary>Data bits; 8 by default.</summary>
    public int DataBits { get; init; } = 8;

    /// <summary>Parity; none by default.</summary>
    public Parity Parity { get; init; } = Parity.None;

    /// <summary>Stop bits; one by default.</summary>
    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>When true, the source keeps retrying after a device disconnect.</summary>
    public bool AutoReconnect { get; init; } = true;

    /// <summary>Delay between reconnect attempts.</summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);
}
