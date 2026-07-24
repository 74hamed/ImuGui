using System.Collections.Concurrent;
using ImuGui.Core.Sources;

namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>Hands out pre-scripted connections in order; each (re)connect consumes one.</summary>
internal sealed class ScriptedSerialPortFactory : ISerialPortConnectionFactory
{
    private readonly ConcurrentQueue<ScriptedSerialConnection> _connections = new();

    internal ScriptedSerialPortFactory(params ScriptedSerialConnection[] connections)
    {
        foreach (ScriptedSerialConnection connection in connections)
        {
            _connections.Enqueue(connection);
        }
    }

    internal IReadOnlyList<string> AvailablePorts { get; init; } = ["COM3", "COM4"];

    internal int CreateCallCount { get; private set; }

    public IReadOnlyList<string> GetAvailablePortNames() => AvailablePorts;

    public ISerialPortConnection Create(SerialSensorOptions options)
    {
        CreateCallCount++;
        return _connections.TryDequeue(out ScriptedSerialConnection? connection)
            ? connection
            : throw new InvalidOperationException(
                "The test script ran out of connections; enqueue more ScriptedSerialConnections.");
    }
}
