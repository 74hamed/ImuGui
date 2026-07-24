using ImuGui.Core.Abstractions;
using ImuGui.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImuGui.Core.Sources;

/// <summary>
/// Replays a CSV recording as a live sample stream at a configurable rate, optionally looping.
/// The file is fully loaded and validated during <see cref="ISensorSource.StartAsync"/>;
/// replay timestamps follow the ideal schedule (sample i at i / rate), so measured dt equals
/// the configured interval.
/// </summary>
public sealed class CsvReplaySensorSource : SensorSourceBase
{
    private const double MaxReplayRateHz = 10_000;

    private readonly CsvReplayOptions _options;
    private readonly IClock _clock;
    private IReadOnlyList<SensorSample> _samples = [];

    /// <summary>Creates the source.</summary>
    /// <param name="options">Replay configuration; the file path is validated at start, not here.</param>
    /// <param name="clock">The clock used for pacing and timestamps.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentOutOfRangeException">The replay rate is not positive or is absurdly high.</exception>
    public CsvReplaySensorSource(
        CsvReplayOptions options, IClock clock, ILogger<CsvReplaySensorSource>? logger = null)
        : base(logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        if (options.ReplayRateHz <= 0 || options.ReplayRateHz > MaxReplayRateHz)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.ReplayRateHz,
                $"Replay rate must be between 0 (exclusive) and {MaxReplayRateHz} Hz.");
        }

        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public override string DisplayName => $"CSV replay: {Path.GetFileName(_options.FilePath)}";

    /// <summary>Number of valid samples loaded from the file (available after a successful start).</summary>
    public int LoadedSampleCount => _samples.Count;

    /// <summary>Number of malformed rows skipped while loading (available after a successful start).</summary>
    public int MalformedRowCount { get; private set; }

    /// <inheritdoc />
    protected override async Task OnStartingAsync(CancellationToken cancellationToken)
    {
        string path = _options.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new SensorSourceException("No CSV file selected. Choose a sensor recording to replay.");
        }

        if (!File.Exists(path))
        {
            throw new SensorSourceException(
                $"CSV file not found: '{path}'. Choose an existing sensor recording to replay.");
        }

        CsvLoadResult result;
        try
        {
            result = await CsvSampleLoader.LoadAsync(path, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new SensorSourceException($"Could not read '{path}': {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SensorSourceException($"Access to '{path}' was denied: {ex.Message}", ex);
        }

        if (result.Samples.Count == 0)
        {
            string detail = result.MalformedRowCount > 0
                ? $" All {result.MalformedRowCount} data rows were malformed"
                  + $" (first issues: {string.Join("; ", result.MalformedRowDetails.Take(3))})."
                : " The file contains no data rows.";
            throw new SensorSourceException(
                $"'{path}' yielded no valid samples.{detail} Expected columns: {SensorLineParser.ExpectedHeader}.");
        }

        _samples = result.Samples;
        MalformedRowCount = result.MalformedRowCount;

        if (result.MalformedRowCount > 0)
        {
            Logger.LogWarning(
                "{DisplayName}: skipped {MalformedCount} malformed rows. Details: {Details}",
                DisplayName, result.MalformedRowCount, string.Join("; ", result.MalformedRowDetails));
        }

        Logger.LogInformation(
            "{DisplayName}: loaded {SampleCount} samples (header: {HadHeader}), replaying at {Rate} Hz, loop: {Loop}.",
            DisplayName, result.Samples.Count, result.HadHeaderRow, _options.ReplayRateHz, _options.Loop);
    }

    /// <inheritdoc />
    protected override async Task RunAcquisitionAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(1.0 / _options.ReplayRateHz);
        TimeSpan startElapsed = _clock.Elapsed;
        long index = 0;

        do
        {
            foreach (SensorSample sample in _samples)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TimeSpan target = startElapsed + (index * interval);
                await _clock.DelayAsync(target - _clock.Elapsed, cancellationToken);

                RaiseSample(sample with { Timestamp = target });
                index++;
            }
        }
        while (_options.Loop && !cancellationToken.IsCancellationRequested);

        Logger.LogInformation("{DisplayName}: replay finished after {Count} samples.", DisplayName, index);
    }
}
