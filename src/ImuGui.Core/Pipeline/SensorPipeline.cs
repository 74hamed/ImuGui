using ImuGui.Core.Calibration;
using ImuGui.Core.Filtering;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Sources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImuGui.Core.Pipeline;

/// <summary>
/// Composes the processing chain — calibration → filter bank → two fusion estimators
/// (one fed unfiltered, one fed filtered data) — and publishes immutable
/// <see cref="ProcessedFrame"/> snapshots. This is the single seam the UI consumes.
/// <para>
/// Per-sample dt is <em>measured</em> from sample timestamps, never assumed.
/// <see cref="FrameProcessed"/> is raised on the acquisition thread; UI code must marshal.
/// </para>
/// </summary>
public sealed class SensorPipeline : IDisposable
{
    private readonly object _processLock = new();
    private IOrientationEstimator _rawEstimator;
    private IOrientationEstimator _filteredEstimator;
    private readonly ICalibrationService _calibrationService;
    private readonly ILogger _logger;
    private ISensorSource? _attachedSource;
    private TimeSpan? _lastTimestamp;
    private volatile ProcessedFrame? _latestFrame;
    private long _frameCount;
    private volatile bool _calibrationEnabled = true;

    /// <summary>Creates the pipeline.</summary>
    /// <param name="filterBank">The per-channel filter bank.</param>
    /// <param name="rawEstimator">The estimator fed unfiltered samples.</param>
    /// <param name="filteredEstimator">The estimator fed filtered samples (a distinct instance).</param>
    /// <param name="calibrationService">The calibration service applied ahead of both paths.</param>
    /// <param name="logger">Optional logger.</param>
    /// <exception cref="ArgumentException">Both estimator parameters are the same instance.</exception>
    public SensorPipeline(
        FilterBank filterBank,
        IOrientationEstimator rawEstimator,
        IOrientationEstimator filteredEstimator,
        ICalibrationService calibrationService,
        ILogger<SensorPipeline>? logger = null)
    {
        FilterBank = filterBank ?? throw new ArgumentNullException(nameof(filterBank));
        _rawEstimator = rawEstimator ?? throw new ArgumentNullException(nameof(rawEstimator));
        _filteredEstimator = filteredEstimator ?? throw new ArgumentNullException(nameof(filteredEstimator));
        _calibrationService = calibrationService ?? throw new ArgumentNullException(nameof(calibrationService));
        _logger = logger ?? NullLogger<SensorPipeline>.Instance;

        if (ReferenceEquals(rawEstimator, filteredEstimator))
        {
            throw new ArgumentException(
                "The raw and filtered paths need two distinct estimator instances.", nameof(filteredEstimator));
        }
    }

    /// <summary>Raised for every processed frame, on the acquisition thread.</summary>
    public event EventHandler<ProcessedFrameEventArgs>? FrameProcessed;

    /// <summary>The filter bank, exposed for retuning from the UI.</summary>
    public FilterBank FilterBank { get; }

    /// <summary>Whether the active calibration profile is applied to incoming samples.</summary>
    public bool CalibrationEnabled
    {
        get => _calibrationEnabled;
        set => _calibrationEnabled = value;
    }

    /// <summary>The most recent frame, or null before the first sample.</summary>
    public ProcessedFrame? LatestFrame => _latestFrame;

    /// <summary>Total frames processed since the last reset.</summary>
    public long FrameCount => Interlocked.Read(ref _frameCount);

    /// <summary>Subscribes to a source (detaching any previous one) and resets processing state.</summary>
    /// <param name="source">The source to consume.</param>
    public void AttachSource(ISensorSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        DetachSource();
        Reset();
        _attachedSource = source;
        source.SampleReceived += OnSampleReceived;
        _logger.LogInformation("Pipeline attached to {DisplayName}.", source.DisplayName);
    }

    /// <summary>Unsubscribes from the current source, if any.</summary>
    public void DetachSource()
    {
        if (_attachedSource is null)
        {
            return;
        }

        _attachedSource.SampleReceived -= OnSampleReceived;
        _logger.LogInformation("Pipeline detached from {DisplayName}.", _attachedSource.DisplayName);
        _attachedSource = null;
    }

    /// <summary>
    /// Swaps the fusion strategy at runtime (e.g. Mahony ↔ complementary). Both new
    /// estimators start fresh and re-initialize from the next sample.
    /// </summary>
    /// <param name="rawEstimator">The new estimator for the unfiltered path.</param>
    /// <param name="filteredEstimator">The new estimator for the filtered path (distinct instance).</param>
    /// <exception cref="ArgumentException">Both parameters are the same instance.</exception>
    public void ReplaceEstimators(IOrientationEstimator rawEstimator, IOrientationEstimator filteredEstimator)
    {
        ArgumentNullException.ThrowIfNull(rawEstimator);
        ArgumentNullException.ThrowIfNull(filteredEstimator);
        if (ReferenceEquals(rawEstimator, filteredEstimator))
        {
            throw new ArgumentException(
                "The raw and filtered paths need two distinct estimator instances.", nameof(filteredEstimator));
        }

        lock (_processLock)
        {
            _rawEstimator = rawEstimator;
            _filteredEstimator = filteredEstimator;
            _rawEstimator.Reset();
            _filteredEstimator.Reset();
        }

        _logger.LogInformation("Fusion estimators replaced with {EstimatorType}.", rawEstimator.GetType().Name);
    }

    /// <summary>Resets filters, both estimators, dt tracking, and the frame counter.</summary>
    public void Reset()
    {
        lock (_processLock)
        {
            FilterBank.ResetAll();
            _rawEstimator.Reset();
            _filteredEstimator.Reset();
            _lastTimestamp = null;
            _latestFrame = null;
            Interlocked.Exchange(ref _frameCount, 0);
        }
    }

    /// <summary>Processes one sample synchronously and returns the resulting frame.</summary>
    /// <param name="sample">The incoming raw sample.</param>
    public ProcessedFrame Process(SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ProcessedFrame frame;
        lock (_processLock)
        {
            TimeSpan deltaTime = TimeSpan.Zero;
            if (_lastTimestamp is { } last)
            {
                TimeSpan measured = sample.Timestamp - last;
                if (measured > TimeSpan.Zero)
                {
                    deltaTime = measured;
                }
                else
                {
                    _logger.LogDebug(
                        "Non-monotonic timestamp ({Last} -> {Current}); treating dt as zero.",
                        last, sample.Timestamp);
                }
            }

            _lastTimestamp = sample.Timestamp;

            SensorSample unfiltered = _calibrationEnabled ? _calibrationService.Correct(sample) : sample;
            SensorSample filtered = FilterBank.Process(unfiltered);

            _rawEstimator.Update(unfiltered, deltaTime);
            _filteredEstimator.Update(filtered, deltaTime);

            frame = new ProcessedFrame(
                sample.Timestamp,
                deltaTime,
                unfiltered,
                filtered,
                _rawEstimator.CurrentOrientation,
                _filteredEstimator.CurrentOrientation);

            _latestFrame = frame;
            Interlocked.Increment(ref _frameCount);
        }

        FrameProcessed?.Invoke(this, new ProcessedFrameEventArgs(frame));
        return frame;
    }

    /// <inheritdoc />
    public void Dispose() => DetachSource();

    private void OnSampleReceived(object? sender, SensorSampleEventArgs e) => Process(e.Sample);
}
