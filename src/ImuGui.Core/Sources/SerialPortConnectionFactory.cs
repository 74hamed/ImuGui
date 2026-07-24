using System.IO.Ports;
using System.Text;

namespace ImuGui.Core.Sources;

/// <summary>The production factory backed by <see cref="SerialPort"/>.</summary>
public sealed class SerialPortConnectionFactory : ISerialPortConnectionFactory
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailablePortNames() =>
        SerialPort.GetPortNames().Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <inheritdoc />
    public ISerialPortConnection Create(SerialSensorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SerialPortConnection(options);
    }

    private sealed class SerialPortConnection : ISerialPortConnection
    {
        private readonly SerialPort _port;

        internal SerialPortConnection(SerialSensorOptions options)
        {
            _port = new SerialPort(
                options.PortName, options.BaudRate, options.Parity, options.DataBits, options.StopBits)
            {
                NewLine = "\n",
                Encoding = Encoding.ASCII,
                ReadTimeout = (int)ReadTimeout.TotalMilliseconds,
                // Many USB-serial IMU boards (Arduino-style) only transmit once DTR is asserted.
                DtrEnable = true,
            };
        }

        public bool IsOpen => _port.IsOpen;

        public void Open() => _port.Open();

        public string ReadLine() => _port.ReadLine().TrimEnd('\r');

        public void Close()
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }

        public void Dispose() => _port.Dispose();
    }
}
