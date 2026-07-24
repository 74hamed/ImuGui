using ImuGui.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImuGui.Core.Sources;

/// <summary>
/// Shared plumbing for <see cref="ISensorSource"/> implementations: lifecycle management,
/// the background acquisition loop, connection-state tracking, and event raising.
/// </summary>
public abstract class SensorSourceBase : ISensorSource
{
    private static readonly TimeSpan DisposeStopTimeout = TimeSpan.FromSeconds(3);

    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _acquisitionCts;
    private Task? _acquisitionTask;
    private SensorConnectionState _connectionState = SensorConnectionState.Disconnected;
    private bool _disposed;

    /// <summary>Initializes the base with an optional logger.</summary>
    /// <param name="logger">The logger; a no-op logger is used when null.</param>
    protected SensorSourceBase(ILogger? logger) => Logger = logger ?? NullLogger.Instance;

    /// <inheritdoc />
    public event EventHandler<SensorSampleEventArgs>? SampleReceived;

    /// <inheritdoc />
    public event EventHandler<SensorConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<SensorSourceErrorEventArgs>? ErrorOccurred;

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public SensorConnectionState ConnectionState => _connectionState;

    /// <summary>The logger available to derived sources.</summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// When true, the acquisition loop runs on a dedicated long-running thread instead of a
    /// thread-pool thread. Sources doing blocking I/O (serial reads) should return true.
    /// </summary>
    protected virtual bool UseDedicatedThread => false;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lifecycleLock)
        {
            if (_connectionState is not (SensorConnectionState.Disconnected or SensorConnectionState.Faulted))
            {
                throw new InvalidOperationException(
                    $"Cannot start '{DisplayName}' while it is {_connectionState}; stop it first.");
            }

            SetConnectionStateCore(SensorConnectionState.Connecting);
        }

        try
        {
            await OnStartingAsync(cancellationToken);
        }
        catch
        {
            SetConnectionState(SensorConnectionState.Disconnected);
            throw;
        }

        lock (_lifecycleLock)
        {
            _acquisitionCts = new CancellationTokenSource();
            CancellationToken token = _acquisitionCts.Token;
            _acquisitionTask = UseDedicatedThread
                ? Task.Factory.StartNew(
                    () => RunLoopGuardedAsync(token),
                    token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap()
                : Task.Run(() => RunLoopGuardedAsync(token), CancellationToken.None);
        }

        SetConnectionState(SensorConnectionState.Connected);
        Logger.LogInformation("Sensor source started: {DisplayName}", DisplayName);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        Task? runningTask;
        lock (_lifecycleLock)
        {
            _acquisitionCts?.Cancel();
            runningTask = _acquisitionTask;
        }

        if (runningTask is not null)
        {
            // The guarded loop never lets exceptions escape; awaiting only joins it.
            await runningTask;
        }

        lock (_lifecycleLock)
        {
            _acquisitionCts?.Dispose();
            _acquisitionCts = null;
            _acquisitionTask = null;
        }

        SetConnectionState(SensorConnectionState.Disconnected);
        Logger.LogInformation("Sensor source stopped: {DisplayName}", DisplayName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Validates configuration and acquires resources (open the port, load the file).
    /// Runs before the acquisition loop starts; throw <see cref="SensorSourceException"/>
    /// with an actionable message on failure.
    /// </summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    protected abstract Task OnStartingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The acquisition loop, executed on a background thread. Implementations should honor
    /// <paramref name="cancellationToken"/> promptly and release their resources on exit.
    /// Returning normally transitions the source to <see cref="SensorConnectionState.Disconnected"/>;
    /// throwing transitions it to <see cref="SensorConnectionState.Faulted"/>.
    /// </summary>
    /// <param name="cancellationToken">Signalled when the source is being stopped.</param>
    protected abstract Task RunAcquisitionAsync(CancellationToken cancellationToken);

    /// <summary>Raises <see cref="SampleReceived"/> (called from the acquisition thread).</summary>
    /// <param name="sample">The acquired sample.</param>
    protected void RaiseSample(SensorSample sample) =>
        SampleReceived?.Invoke(this, new SensorSampleEventArgs(sample));

    /// <summary>Transitions the connection state and raises <see cref="ConnectionStateChanged"/>.</summary>
    /// <param name="newState">The new state.</param>
    protected void SetConnectionState(SensorConnectionState newState)
    {
        lock (_lifecycleLock)
        {
            SetConnectionStateCore(newState);
        }
    }

    /// <summary>Raises <see cref="ErrorOccurred"/> with a user-presentable message.</summary>
    /// <param name="message">The user-presentable message.</param>
    /// <param name="exception">The underlying exception, when one exists.</param>
    protected void RaiseError(string message, Exception? exception = null)
    {
        Logger.LogWarning(exception, "{DisplayName}: {Message}", DisplayName, message);
        ErrorOccurred?.Invoke(this, new SensorSourceErrorEventArgs(message, exception));
    }

    /// <summary>Stops the source synchronously as part of disposal.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (!StopAsync().Wait(DisposeStopTimeout))
            {
                Logger.LogWarning(
                    "{DisplayName}: acquisition loop did not stop within {Timeout} during dispose.",
                    DisplayName, DisposeStopTimeout);
            }
        }
        catch (AggregateException ex)
        {
            Logger.LogWarning(ex.GetBaseException(), "{DisplayName}: error while stopping during dispose.", DisplayName);
        }
    }

    private async Task RunLoopGuardedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunAcquisitionAsync(cancellationToken);
            SetConnectionState(SensorConnectionState.Disconnected);
        }
        catch (OperationCanceledException)
        {
            // Normal stop path; StopAsync sets the final state.
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{DisplayName}: acquisition failed.", DisplayName);
            RaiseError(ex is SensorSourceException ? ex.Message : $"Acquisition failed: {ex.Message}", ex);
            SetConnectionState(SensorConnectionState.Faulted);
        }
    }

    private void SetConnectionStateCore(SensorConnectionState newState)
    {
        SensorConnectionState previous = _connectionState;
        if (previous == newState)
        {
            return;
        }

        _connectionState = newState;
        Logger.LogDebug("{DisplayName}: {Previous} -> {New}", DisplayName, previous, newState);
        ConnectionStateChanged?.Invoke(this, new SensorConnectionStateChangedEventArgs(previous, newState));
    }
}
