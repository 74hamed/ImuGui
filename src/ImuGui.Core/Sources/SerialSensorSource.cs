using ImuGui.Core.Abstractions;
using ImuGui.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImuGui.Core.Sources;

/// <summary>
/// Reads sensor samples from a serial (COM) port: opens the configured port, reads
/// newline-terminated protocol lines on a dedicated background thread, parses them via
/// <see cref="SensorLineParser"/>, and stamps arrival time from the injected clock.
/// Handles open failures with actionable messages, tolerates malformed lines, and
/// optionally reconnects after device loss.
/// </summary>
public sealed class SerialSensorSource : SensorSourceBase
{
    private const int MalformedLogFirst = 5;
    private const int MalformedLogEvery = 500;

    private readonly SerialSensorOptions _options;
    private readonly ISerialPortConnectionFactory _connectionFactory;
    private readonly IClock _clock;
    private ISerialPortConnection? _connection;
    private long _malformedLineCount;

    /// <summary>Creates the source.</summary>
    /// <param name="options">Port configuration.</param>
    /// <param name="connectionFactory">Factory used to enumerate and open ports.</param>
    /// <param name="clock">The clock used to timestamp samples.</param>
    /// <param name="logger">Optional logger.</param>
    public SerialSensorSource(
        SerialSensorOptions options,
        ISerialPortConnectionFactory connectionFactory,
        IClock clock,
        ILogger<SerialSensorSource>? logger = null)
        : base(logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _connectionFactory = connectionFactory;
        _clock = clock;
    }

    /// <inheritdoc />
    public override string DisplayName => $"Serial: {_options.PortName} @ {_options.BaudRate} baud";

    /// <summary>How many received lines failed to parse and were skipped.</summary>
    public long MalformedLineCount => Interlocked.Read(ref _malformedLineCount);

    /// <inheritdoc />
    protected override bool UseDedicatedThread => true;

    /// <inheritdoc />
    protected override Task OnStartingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PortName))
        {
            throw new SensorSourceException(
                $"No COM port selected. {DescribeAvailablePorts()}");
        }

        Interlocked.Exchange(ref _malformedLineCount, 0);
        _connection = _connectionFactory.Create(_options);
        try
        {
            _connection.Open();
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException)
        {
            _connection.Dispose();
            _connection = null;
            throw new SensorSourceException(
                $"Could not open {_options.PortName} at {_options.BaudRate} baud: {ex.Message} "
                + DescribeAvailablePorts(),
                ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task RunAcquisitionAsync(CancellationToken cancellationToken)
    {
        // The first line after opening a port is frequently a partial frame; discard it.
        bool discardNextLine = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string line;
                try
                {
                    line = _connection!.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue; // No data yet; loop to honor cancellation.
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException)
                {
                    if (!_options.AutoReconnect)
                    {
                        throw new SensorSourceException(
                            $"Serial connection to {_options.PortName} was lost: {ex.Message}", ex);
                    }

                    RaiseError($"Connection to {_options.PortName} lost ({ex.Message}); reconnecting…", ex);
                    SetConnectionState(SensorConnectionState.Reconnecting);
                    await ReconnectAsync(cancellationToken);
                    discardNextLine = true;
                    continue;
                }

                if (discardNextLine)
                {
                    discardNextLine = false;
                    continue;
                }

                if (SensorLineParser.IsLikelyHeader(line))
                {
                    continue; // Devices may echo a header line on boot; not an error.
                }

                if (!SensorLineParser.TryParse(line, out SensorSample? sample, out string? parseError))
                {
                    long count = Interlocked.Increment(ref _malformedLineCount);
                    if (count <= MalformedLogFirst || count % MalformedLogEvery == 0)
                    {
                        Logger.LogWarning(
                            "{DisplayName}: skipped malformed line #{Count}: {Reason}",
                            DisplayName, count, parseError);
                    }

                    continue;
                }

                RaiseSample(sample with { Timestamp = _clock.Elapsed });
            }
        }
        finally
        {
            CloseConnectionQuietly();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            CloseConnectionQuietly();
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        CloseConnectionQuietly();

        int attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _clock.DelayAsync(_options.ReconnectDelay, cancellationToken);

            attempt++;
            try
            {
                _connection = _connectionFactory.Create(_options);
                _connection.Open();
                SetConnectionState(SensorConnectionState.Connected);
                Logger.LogInformation(
                    "{DisplayName}: reconnected after {Attempts} attempt(s).", DisplayName, attempt);
                return;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException)
            {
                CloseConnectionQuietly();
                Logger.LogDebug(
                    "{DisplayName}: reconnect attempt {Attempt} failed: {Reason}",
                    DisplayName, attempt, ex.Message);
            }
        }
    }

    private void CloseConnectionQuietly()
    {
        ISerialPortConnection? connection = Interlocked.Exchange(ref _connection, null);
        if (connection is null)
        {
            return;
        }

        try
        {
            connection.Close();
            connection.Dispose();
        }
        catch (IOException ex)
        {
            Logger.LogDebug(ex, "{DisplayName}: ignoring error while closing the port.", DisplayName);
        }
    }

    private string DescribeAvailablePorts()
    {
        IReadOnlyList<string> ports = _connectionFactory.GetAvailablePortNames();
        return ports.Count > 0
            ? $"Available ports: {string.Join(", ", ports)}."
            : "No serial ports were detected on this machine.";
    }
}
