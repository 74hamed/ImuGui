namespace ImuGui.Core.Sources;

/// <summary>Enumerates serial ports and creates connections; the seam for serial testing.</summary>
public interface ISerialPortConnectionFactory
{
    /// <summary>Names of the serial ports currently present on the machine.</summary>
    IReadOnlyList<string> GetAvailablePortNames();

    /// <summary>Creates an unopened connection for the given options.</summary>
    /// <param name="options">Port name, baud rate, and framing settings.</param>
    ISerialPortConnection Create(SerialSensorOptions options);
}
