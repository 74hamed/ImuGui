using System.Collections.Concurrent;
using ImuGui.Core.Sources;

namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>
/// A scripted fake serial connection: reads are served from a queue of actions (return a
/// line or throw); when the script drains, reads behave like an idle port (timeout).
/// </summary>
internal sealed class ScriptedSerialConnection : ISerialPortConnection
{
    private readonly ConcurrentQueue<Func<string>> _reads = new();

    internal Exception? OpenException { get; init; }

    internal bool WasClosed { get; private set; }

    internal bool WasDisposed { get; private set; }

    public bool IsOpen { get; private set; }

    internal ScriptedSerialConnection EnqueueLine(string line)
    {
        _reads.Enqueue(() => line);
        return this;
    }

    internal ScriptedSerialConnection EnqueueThrow(Exception exception)
    {
        _reads.Enqueue(() => throw exception);
        return this;
    }

    public void Open()
    {
        if (OpenException is not null)
        {
            throw OpenException;
        }

        IsOpen = true;
    }

    public string ReadLine()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The port is not open.");
        }

        if (_reads.TryDequeue(out Func<string>? read))
        {
            return read();
        }

        // Idle port: brief pause keeps the acquisition loop from hot-spinning in tests.
        Thread.Sleep(5);
        throw new TimeoutException();
    }

    public void Close()
    {
        IsOpen = false;
        WasClosed = true;
    }

    public void Dispose()
    {
        IsOpen = false;
        WasDisposed = true;
    }
}
