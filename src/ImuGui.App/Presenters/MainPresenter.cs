using ImuGui.App.Models;
using ImuGui.App.Settings;
using ImuGui.Core.Abstractions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Filtering;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using ImuGui.Core.Sources;
using Microsoft.Extensions.Logging;

namespace ImuGui.App.Presenters;

/// <summary>
/// Mediates between the main view and the Core: source lifecycle (connect/disconnect),
/// the global raw/filtered toggle, filter retuning, fusion strategy selection, and
/// settings round-tripping. Forms hold no domain logic; it all lives here or in Core.
/// </summary>
public sealed class MainPresenter : IDisposable
{
    private readonly IMainView _view;
    private readonly ISettingsService _settings;
    private readonly ISerialPortConnectionFactory _serialPortFactory;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainPresenter> _logger;
    private ISensorSource? _activeSource;

    /// <summary>Creates the presenter.</summary>
    public MainPresenter(
        IMainView view,
        SensorPipeline pipeline,
        ISettingsService settings,
        ISerialPortConnectionFactory serialPortFactory,
        ICalibrationService calibrationService,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _serialPortFactory = serialPortFactory ?? throw new ArgumentNullException(nameof(serialPortFactory));
        CalibrationService = calibrationService ?? throw new ArgumentNullException(nameof(calibrationService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<MainPresenter>();
    }

    /// <summary>Forwards raw (pre-pipeline) samples from the active source; used by calibration capture.</summary>
    public event EventHandler<SensorSampleEventArgs>? RawSampleReceived;

    /// <summary>The processing pipeline (panels read frames from it).</summary>
    public SensorPipeline Pipeline { get; }

    /// <summary>The calibration service (the calibration dialog works against it).</summary>
    public ICalibrationService CalibrationService { get; }

    /// <summary>The global raw/filtered toggle. Views consult this on every render tick.</summary>
    public bool FilteringEnabled { get; private set; }

    /// <summary>Whether a source is currently attached (any non-disconnected state).</summary>
    public bool IsSourceActive => _activeSource is not null;

    /// <summary>The current settings snapshot (read-only access for panels).</summary>
    public UserSettings Settings => _settings.Current;

    /// <summary>Loads persisted settings into the view and prepares initial state.</summary>
    public void Initialize()
    {
        UserSettings settings = _settings.Current;
        _view.SelectedSourceMode = settings.SourceMode;
        _view.CsvFilePath = settings.CsvFilePath;
        _view.ReplayRateHz = settings.ReplayRateHz;
        _view.LoopReplay = settings.LoopReplay;
        _view.SelectedBaudRate = settings.BaudRate;
        _view.FilteringEnabled = settings.FilteringEnabled;
        _view.CalibrationEnabled = settings.CalibrationEnabled;
        _view.SelectedEstimatorKind = settings.EstimatorKind;

        FilteringEnabled = settings.FilteringEnabled;
        Pipeline.CalibrationEnabled = settings.CalibrationEnabled;
        ApplyEstimatorKind(settings.EstimatorKind);
        Pipeline.FilterBank.RetuneAll(settings.FilterConfig, RetuneBehavior.ResetState);

        RefreshSerialPorts();
        _view.SelectedSerialPort = settings.SerialPortName.Length > 0 ? settings.SerialPortName : null;
        _view.SetConnectionState(SensorConnectionState.Disconnected, "No source");
    }

    /// <summary>Builds a source from the view's configuration and starts acquisition.</summary>
    public async Task ConnectAsync()
    {
        if (_activeSource is not null)
        {
            await DisconnectAsync();
        }

        PullSourceConfigurationFromView();

        ISensorSource source;
        try
        {
            source = CreateSourceFromSettings();
        }
        catch (SensorSourceException ex)
        {
            _view.ShowError("Cannot start source", ex.Message);
            return;
        }

        source.ConnectionStateChanged += OnSourceConnectionStateChanged;
        source.ErrorOccurred += OnSourceError;
        source.SampleReceived += OnSourceSampleReceived;
        Pipeline.AttachSource(source);
        _activeSource = source;

        try
        {
            await source.StartAsync();
            _view.SetStatusMessage($"{source.DisplayName} started.");
        }
        catch (Exception ex) when (ex is SensorSourceException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Source failed to start.");
            await DisconnectAsync();
            _view.ShowError("Cannot connect", ex.Message);
        }
    }

    /// <summary>Stops and releases the active source.</summary>
    public async Task DisconnectAsync()
    {
        ISensorSource? source = _activeSource;
        if (source is null)
        {
            return;
        }

        _activeSource = null;
        Pipeline.DetachSource();
        source.ConnectionStateChanged -= OnSourceConnectionStateChanged;
        source.ErrorOccurred -= OnSourceError;
        source.SampleReceived -= OnSourceSampleReceived;

        try
        {
            await source.StopAsync();
        }
        finally
        {
            source.Dispose();
        }

        _view.SetConnectionState(SensorConnectionState.Disconnected, "No source");
        _view.SetStatusMessage("Disconnected.");
    }

    /// <summary>Re-enumerates COM ports into the view.</summary>
    public void RefreshSerialPorts() =>
        _view.SetAvailableSerialPorts(_serialPortFactory.GetAvailablePortNames());

    /// <summary>Applies the global raw/filtered toggle.</summary>
    /// <param name="enabled">True to show filtered data everywhere.</param>
    public void SetFilteringEnabled(bool enabled)
    {
        FilteringEnabled = enabled;
        _settings.Update(s => s with { FilteringEnabled = enabled });
    }

    /// <summary>Applies calibration on/off to the pipeline.</summary>
    /// <param name="enabled">True to correct samples with the active profile.</param>
    public void SetCalibrationEnabled(bool enabled)
    {
        Pipeline.CalibrationEnabled = enabled;
        _settings.Update(s => s with { CalibrationEnabled = enabled });
    }

    /// <summary>Applies new Kalman tuning to the whole filter bank.</summary>
    /// <param name="config">The validated parameters.</param>
    /// <param name="behavior">Reset or preserve runtime state.</param>
    public void ApplyFilterTuning(FilterConfig config, RetuneBehavior behavior)
    {
        Pipeline.FilterBank.RetuneAll(config, behavior);
        _settings.Update(s => s with { FilterConfig = config });
        _view.SetStatusMessage(
            $"Filter retuned (Q={config.ProcessNoise}, R={config.MeasurementNoise}, {behavior}).");
    }

    /// <summary>Switches the fusion strategy at runtime.</summary>
    /// <param name="kind">The strategy to use.</param>
    public void SetEstimatorKind(OrientationEstimatorKind kind)
    {
        ApplyEstimatorKind(kind);
        _settings.Update(s => s with { EstimatorKind = kind });
        _view.SetStatusMessage($"Fusion strategy: {kind}.");
    }

    /// <summary>Copies the view's configuration into settings and persists everything.</summary>
    public void SaveSettings()
    {
        PullSourceConfigurationFromView();
        _settings.Save();
    }

    /// <summary>Stops acquisition synchronously (bounded); used on window close.</summary>
    public void ShutDown()
    {
        try
        {
            if (!DisconnectAsync().Wait(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Source did not stop within 5 s during shutdown.");
            }
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex.GetBaseException(), "Error while stopping the source during shutdown.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => ShutDown();

    private void ApplyEstimatorKind(OrientationEstimatorKind kind) =>
        Pipeline.ReplaceEstimators(
            OrientationEstimatorFactory.Create(kind), OrientationEstimatorFactory.Create(kind));

    private void PullSourceConfigurationFromView() =>
        _settings.Update(s => s with
        {
            SourceMode = _view.SelectedSourceMode,
            CsvFilePath = _view.CsvFilePath,
            ReplayRateHz = _view.ReplayRateHz,
            LoopReplay = _view.LoopReplay,
            SerialPortName = _view.SelectedSerialPort ?? string.Empty,
            BaudRate = _view.SelectedBaudRate,
        });

    private ISensorSource CreateSourceFromSettings()
    {
        UserSettings settings = _settings.Current;
        switch (settings.SourceMode)
        {
            case SourceMode.CsvReplay:
                var replayOptions = new CsvReplayOptions
                {
                    FilePath = settings.CsvFilePath,
                    ReplayRateHz = settings.ReplayRateHz,
                    Loop = settings.LoopReplay,
                };
                return new CsvReplaySensorSource(
                    replayOptions, _clock, _loggerFactory.CreateLogger<CsvReplaySensorSource>());

            case SourceMode.Serial:
                if (string.IsNullOrWhiteSpace(settings.SerialPortName))
                {
                    throw new SensorSourceException(
                        "No COM port selected. Pick a port (use Refresh if the list is empty).");
                }

                var serialOptions = new SerialSensorOptions
                {
                    PortName = settings.SerialPortName,
                    BaudRate = settings.BaudRate,
                };
                return new SerialSensorSource(
                    serialOptions, _serialPortFactory, _clock,
                    _loggerFactory.CreateLogger<SerialSensorSource>());

            default:
                throw new SensorSourceException($"Unknown source mode: {settings.SourceMode}.");
        }
    }

    private void OnSourceConnectionStateChanged(object? sender, SensorConnectionStateChangedEventArgs e)
    {
        string name = (sender as ISensorSource)?.DisplayName ?? "Source";
        _view.SetConnectionState(e.NewState, name);
    }

    private void OnSourceError(object? sender, SensorSourceErrorEventArgs e) =>
        _view.SetStatusMessage(e.Message);

    private void OnSourceSampleReceived(object? sender, SensorSampleEventArgs e) =>
        RawSampleReceived?.Invoke(this, e);
}
