using ImuGui.Core.Models;

namespace ImuGui.Core.Sources;

/// <summary>
/// A source of IMU samples (CSV replay, serial device, …).
/// <para>
/// Threading contract: <see cref="SampleReceived"/>, <see cref="ConnectionStateChanged"/>, and
/// <see cref="ErrorOccurred"/> are raised on a background acquisition thread — never on the UI
/// thread. Subscribers are responsible for marshalling to their own context.
/// </para>
/// </summary>
public interface ISensorSource : IDisposable
{
    /// <summary>A human-readable name for status displays (e.g. "Serial: COM3 @ 115200").</summary>
    string DisplayName { get; }

    /// <summary>The current connection state.</summary>
    SensorConnectionState ConnectionState { get; }

    /// <summary>Raised for every successfully acquired sample.</summary>
    event EventHandler<SensorSampleEventArgs>? SampleReceived;

    /// <summary>Raised whenever <see cref="ConnectionState"/> changes.</summary>
    event EventHandler<SensorConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>Raised for recoverable runtime errors (e.g. device disconnect while reconnecting).</summary>
    event EventHandler<SensorSourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Validates configuration, acquires resources, and begins delivering samples.
    /// Throws <see cref="SensorSourceException"/> with an actionable message when the
    /// source cannot start (missing file, unopenable port, no valid data).
    /// </summary>
    /// <param name="cancellationToken">Cancels startup (not the subsequent acquisition).</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops acquisition and releases the underlying device/file. Idempotent.</summary>
    Task StopAsync();
}
