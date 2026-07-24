using System.Diagnostics;
using ImuGui.App.Dialogs;
using ImuGui.App.Models;
using ImuGui.App.Presenters;
using ImuGui.App.Settings;
using ImuGui.App.Views;
using ImuGui.Core.Calibration;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using Orientation = System.Windows.Forms.Orientation;

namespace ImuGui.App;

/// <summary>
/// The main window: a passive <see cref="IMainView"/> plus layout and render-tick plumbing.
/// All domain logic lives in <see cref="MainPresenter"/> and Core.
/// <para>
/// Threading: data acquisition runs on background threads; this form's render timer only
/// <em>reads</em> the pipeline's latest frame at ~30 FPS, fully decoupling data rate from
/// render rate. Presenter callbacks arriving from background threads are marshalled here.
/// </para>
/// </summary>
public sealed class MainForm : Form, IMainView
{
    private static readonly int[] StandardBaudRates =
        [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

    private readonly SensorPipeline _pipeline;
    private readonly ISettingsService _settingsService;
    private readonly ICalibrationService _calibrationService;

    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly Stopwatch _rateStopwatch = Stopwatch.StartNew();
    private long _lastRateFrameCount;
    private double _measuredSampleRateHz;

    private MainPresenter? _presenter;

    // Source controls
    private readonly RadioButton _csvModeRadioButton;
    private readonly RadioButton _serialModeRadioButton;
    private readonly TextBox _csvPathTextBox;
    private readonly Button _browseCsvButton;
    private readonly NumericUpDown _replayRateUpDown;
    private readonly CheckBox _loopReplayCheckBox;
    private readonly ComboBox _serialPortComboBox;
    private readonly Button _refreshPortsButton;
    private readonly ComboBox _baudRateComboBox;

    // Connection / processing controls
    private readonly Button _connectButton;
    private readonly ConnectionStatusIndicator _connectionStatusIndicator;
    private readonly CheckBox _filterEnabledCheckBox;
    private readonly Button _tuneFiltersButton;
    private readonly ComboBox _estimatorComboBox;
    private readonly Button _calibrateButton;
    private readonly CheckBox _calibrationEnabledCheckBox;

    // Panels
    private readonly ReadoutsPanel _readoutsPanel = new();
    private readonly InstrumentsPanel _instrumentsPanel = new();
    private readonly ChartsPanel _chartsPanel = new();
    private readonly CubeViewsPanel _cubeViewsPanel = new();
    private readonly EnvironmentPanel _environmentPanel = new();

    // Status bar
    private readonly ToolStripStatusLabel _sampleRateStatusLabel = new("0.0 Hz");
    private readonly ToolStripStatusLabel _frameCountStatusLabel = new("0 frames");
    private readonly ToolStripStatusLabel _messageStatusLabel = new(string.Empty)
    {
        Spring = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    public MainForm(
        SensorPipeline pipeline, ISettingsService settingsService, ICalibrationService calibrationService)
    {
        _pipeline = pipeline;
        _settingsService = settingsService;
        _calibrationService = calibrationService;

        Text = "ImuGui — IMU Sensor Visualization";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1150, 780);

        // ── Source group ────────────────────────────────────────────────────
        _csvModeRadioButton = new RadioButton { Text = "CSV replay", Checked = true, AutoSize = true };
        _serialModeRadioButton = new RadioButton { Text = "Serial (COM)", AutoSize = true };
        _csvPathTextBox = new TextBox { Width = 240 };
        _browseCsvButton = new Button { Text = "Browse…", AutoSize = true };
        _replayRateUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1000,
            Value = 50,
            Width = 64,
        };
        _loopReplayCheckBox = new CheckBox { Text = "Loop", Checked = true, AutoSize = true };
        _serialPortComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        _refreshPortsButton = new Button { Text = "Refresh", AutoSize = true };
        _baudRateComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        foreach (int baudRate in StandardBaudRates)
        {
            _baudRateComboBox.Items.Add(baudRate);
        }

        _baudRateComboBox.SelectedItem = 115200;

        // ── Connection group ────────────────────────────────────────────────
        _connectButton = new Button { Text = "Connect", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        _connectionStatusIndicator = new ConnectionStatusIndicator();

        // ── Filtering / fusion group ────────────────────────────────────────
        _filterEnabledCheckBox = new CheckBox { Text = "Use filtered data", Checked = true, AutoSize = true };
        _tuneFiltersButton = new Button { Text = "Tune filters…", AutoSize = true };
        _estimatorComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        _estimatorComboBox.Items.Add(new EstimatorChoice(
            OrientationEstimatorKind.MahonyQuaternion, "Mahony (quaternion)"));
        _estimatorComboBox.Items.Add(new EstimatorChoice(
            OrientationEstimatorKind.EulerComplementary, "Complementary (Euler)"));
        _estimatorComboBox.SelectedIndex = 0;

        // ── Calibration group ───────────────────────────────────────────────
        _calibrateButton = new Button { Text = "Calibrate…", AutoSize = true };
        _calibrationEnabledCheckBox = new CheckBox { Text = "Apply calibration", Checked = true, AutoSize = true };

        BuildLayout();
        WireLocalEvents();

        _renderTimer = new System.Windows.Forms.Timer { Interval = 33 }; // Render cadence only — never the data clock.
        _renderTimer.Tick += (_, _) => OnRenderTick();
    }

    // ───────────────────────────── IMainView ─────────────────────────────────

    public SourceMode SelectedSourceMode
    {
        get => _serialModeRadioButton.Checked ? SourceMode.Serial : SourceMode.CsvReplay;
        set
        {
            _csvModeRadioButton.Checked = value == SourceMode.CsvReplay;
            _serialModeRadioButton.Checked = value == SourceMode.Serial;
        }
    }

    public string CsvFilePath
    {
        get => _csvPathTextBox.Text;
        set => _csvPathTextBox.Text = value;
    }

    public double ReplayRateHz
    {
        get => (double)_replayRateUpDown.Value;
        set => _replayRateUpDown.Value = Math.Clamp((decimal)value, _replayRateUpDown.Minimum, _replayRateUpDown.Maximum);
    }

    public bool LoopReplay
    {
        get => _loopReplayCheckBox.Checked;
        set => _loopReplayCheckBox.Checked = value;
    }

    public string? SelectedSerialPort
    {
        get => _serialPortComboBox.SelectedItem as string;
        set
        {
            if (value is not null && _serialPortComboBox.Items.Contains(value))
            {
                _serialPortComboBox.SelectedItem = value;
            }
        }
    }

    public int SelectedBaudRate
    {
        get => _baudRateComboBox.SelectedItem is int baudRate ? baudRate : 115200;
        set
        {
            if (_baudRateComboBox.Items.Contains(value))
            {
                _baudRateComboBox.SelectedItem = value;
            }
        }
    }

    public bool FilteringEnabled
    {
        get => _filterEnabledCheckBox.Checked;
        set => _filterEnabledCheckBox.Checked = value;
    }

    public bool CalibrationEnabled
    {
        get => _calibrationEnabledCheckBox.Checked;
        set => _calibrationEnabledCheckBox.Checked = value;
    }

    public OrientationEstimatorKind SelectedEstimatorKind
    {
        get => _estimatorComboBox.SelectedItem is EstimatorChoice choice
            ? choice.Kind
            : OrientationEstimatorKind.MahonyQuaternion;
        set
        {
            foreach (object item in _estimatorComboBox.Items)
            {
                if (item is EstimatorChoice choice && choice.Kind == value)
                {
                    _estimatorComboBox.SelectedItem = item;
                    return;
                }
            }
        }
    }

    public void SetAvailableSerialPorts(IReadOnlyList<string> ports) => OnUiThread(() =>
    {
        string? previousSelection = SelectedSerialPort;
        _serialPortComboBox.Items.Clear();
        foreach (string port in ports)
        {
            _serialPortComboBox.Items.Add(port);
        }

        if (previousSelection is not null && ports.Contains(previousSelection))
        {
            _serialPortComboBox.SelectedItem = previousSelection;
        }
        else if (ports.Count > 0)
        {
            _serialPortComboBox.SelectedIndex = 0;
        }
    });

    public void SetConnectionState(SensorConnectionState state, string sourceDisplayName) => OnUiThread(() =>
    {
        _connectionStatusIndicator.SetState(state, sourceDisplayName);
        bool active = state is SensorConnectionState.Connected
            or SensorConnectionState.Connecting
            or SensorConnectionState.Reconnecting;
        _connectButton.Text = active ? "Disconnect" : "Connect";
        SetSourceConfigurationEnabled(!active);
    });

    public void SetStatusMessage(string message) => OnUiThread(() => _messageStatusLabel.Text = message);

    public void ShowError(string title, string message) => OnUiThread(() =>
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error));

    public void ShowWarning(string title, string message) => OnUiThread(() =>
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning));

    // ───────────────────────────── Lifecycle ─────────────────────────────────

    /// <summary>Attaches the presenter and brings the UI to its initial state.</summary>
    public void AttachPresenter(MainPresenter presenter)
    {
        _presenter = presenter;
        presenter.Initialize();

        UserSettings settings = presenter.Settings;
        _chartsPanel.Attach(_pipeline, settings);
        _cubeViewsPanel.Initialize(settings);
        _environmentPanel.ShowGrid = settings.ShowEnvironmentGrid;

        UpdateSourceModeEnablement();
        _renderTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _renderTimer.Stop();

        if (_presenter is { } presenter)
        {
            PersistPanelPreferences();
            presenter.SaveSettings();
            presenter.ShutDown(); // Acquisition stops before the window goes away.
        }

        base.OnFormClosing(e);
    }

    // ───────────────────────────── Internals ─────────────────────────────────

    private void BuildLayout()
    {
        var sourceGroup = new GroupBox { Text = "Source", AutoSize = true, Padding = new Padding(8) };
        var sourceFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, MaximumSize = new Size(520, 0) };
        sourceFlow.Controls.Add(_csvModeRadioButton);
        sourceFlow.Controls.Add(_csvPathTextBox);
        sourceFlow.Controls.Add(_browseCsvButton);
        sourceFlow.Controls.Add(new Label { Text = "Rate (Hz):", AutoSize = true, Padding = new Padding(4, 6, 0, 0) });
        sourceFlow.Controls.Add(_replayRateUpDown);
        sourceFlow.Controls.Add(_loopReplayCheckBox);
        sourceFlow.Controls.Add(_serialModeRadioButton);
        sourceFlow.Controls.Add(_serialPortComboBox);
        sourceFlow.Controls.Add(_refreshPortsButton);
        sourceFlow.Controls.Add(new Label { Text = "Baud:", AutoSize = true, Padding = new Padding(4, 6, 0, 0) });
        sourceFlow.Controls.Add(_baudRateComboBox);
        sourceGroup.Controls.Add(sourceFlow);

        var connectionGroup = new GroupBox { Text = "Connection", AutoSize = true, Padding = new Padding(8) };
        var connectionFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        connectionFlow.Controls.Add(_connectButton);
        connectionFlow.Controls.Add(_connectionStatusIndicator);
        connectionGroup.Controls.Add(connectionFlow);

        var processingGroup = new GroupBox { Text = "Processing", AutoSize = true, Padding = new Padding(8) };
        var processingFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, MaximumSize = new Size(420, 0) };
        processingFlow.Controls.Add(_filterEnabledCheckBox);
        processingFlow.Controls.Add(_tuneFiltersButton);
        processingFlow.Controls.Add(new Label { Text = "Fusion:", AutoSize = true, Padding = new Padding(4, 6, 0, 0) });
        processingFlow.Controls.Add(_estimatorComboBox);
        processingFlow.Controls.Add(_calibrateButton);
        processingFlow.Controls.Add(_calibrationEnabledCheckBox);
        processingGroup.Controls.Add(processingFlow);

        var controlBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(6),
        };
        controlBar.Controls.Add(sourceGroup);
        controlBar.Controls.Add(connectionGroup);
        controlBar.Controls.Add(processingGroup);

        var dashboardSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 55,
        };
        dashboardSplit.Panel1.Controls.Add(_instrumentsPanel);
        dashboardSplit.Panel2.Controls.Add(_readoutsPanel);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateTab("Dashboard", dashboardSplit));
        tabs.TabPages.Add(CreateTab("Charts", _chartsPanel));
        tabs.TabPages.Add(CreateTab("3D Views", _cubeViewsPanel));
        tabs.TabPages.Add(CreateTab("Environment", _environmentPanel));

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_messageStatusLabel);
        statusStrip.Items.Add(_sampleRateStatusLabel);
        statusStrip.Items.Add(_frameCountStatusLabel);

        Controls.Add(tabs);
        Controls.Add(controlBar);
        Controls.Add(statusStrip);
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var page = new TabPage(title);
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }

    private void WireLocalEvents()
    {
        _connectButton.Click += async (_, _) => await OnConnectButtonClickedAsync();
        _browseCsvButton.Click += (_, _) => OnBrowseCsvClicked();
        _refreshPortsButton.Click += (_, _) => _presenter?.RefreshSerialPorts();
        _csvModeRadioButton.CheckedChanged += (_, _) => UpdateSourceModeEnablement();
        _serialModeRadioButton.CheckedChanged += (_, _) => UpdateSourceModeEnablement();
        _filterEnabledCheckBox.CheckedChanged += (_, _) =>
            _presenter?.SetFilteringEnabled(_filterEnabledCheckBox.Checked);
        _calibrationEnabledCheckBox.CheckedChanged += (_, _) =>
            _presenter?.SetCalibrationEnabled(_calibrationEnabledCheckBox.Checked);
        _estimatorComboBox.SelectedIndexChanged += (_, _) =>
            _presenter?.SetEstimatorKind(SelectedEstimatorKind);
        _tuneFiltersButton.Click += (_, _) => OnTuneFiltersClicked();
        _calibrateButton.Click += (_, _) => OnCalibrateClicked();
        _environmentPanel.GridVisibilityChanged += (_, _) =>
            _settingsService.Update(s => s with { ShowEnvironmentGrid = _environmentPanel.ShowGrid });
    }

    private async Task OnConnectButtonClickedAsync()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }

        _connectButton.Enabled = false;
        try
        {
            if (presenter.IsSourceActive)
            {
                await presenter.DisconnectAsync();
            }
            else
            {
                await presenter.ConnectAsync();
            }
        }
        finally
        {
            _connectButton.Enabled = true;
        }
    }

    private void OnBrowseCsvClicked()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a sensor recording",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = _csvPathTextBox.Text,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _csvPathTextBox.Text = dialog.FileName;
        }
    }

    private void OnTuneFiltersClicked()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }

        using var dialog = new FilterTuningDialog(_pipeline.FilterBank.CurrentConfig);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ResultConfig is { } config)
        {
            presenter.ApplyFilterTuning(config, dialog.ResultBehavior);
        }
    }

    private void OnCalibrateClicked()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }

        using var dialog = new CalibrationDialog(_calibrationService);
        void ForwardSample(object? sender, Core.Sources.SensorSampleEventArgs e) =>
            dialog.SubmitSample(e.Sample);

        presenter.RawSampleReceived += ForwardSample;
        try
        {
            dialog.ShowDialog(this);
        }
        finally
        {
            presenter.RawSampleReceived -= ForwardSample;
        }
    }

    private void OnRenderTick()
    {
        ProcessedFrame? frame = _pipeline.LatestFrame;
        bool useFiltered = _filterEnabledCheckBox.Checked;

        _readoutsPanel.RenderFrame(frame, useFiltered);
        _instrumentsPanel.RenderFrame(frame, useFiltered);
        _cubeViewsPanel.RenderTick(frame);
        _environmentPanel.RenderFrame(frame, useFiltered);
        _chartsPanel.RenderTick(useFiltered);

        UpdateMeasuredSampleRate();
        _frameCountStatusLabel.Text = $"{_pipeline.FrameCount} frames";
    }

    private void UpdateMeasuredSampleRate()
    {
        if (_rateStopwatch.Elapsed < TimeSpan.FromSeconds(1))
        {
            return;
        }

        long frameCount = _pipeline.FrameCount;
        _measuredSampleRateHz = (frameCount - _lastRateFrameCount) / _rateStopwatch.Elapsed.TotalSeconds;
        _lastRateFrameCount = frameCount;
        _rateStopwatch.Restart();
        _sampleRateStatusLabel.Text = $"{_measuredSampleRateHz:F1} Hz";
    }

    private void UpdateSourceModeEnablement()
    {
        bool csvMode = _csvModeRadioButton.Checked;
        _csvPathTextBox.Enabled = csvMode;
        _browseCsvButton.Enabled = csvMode;
        _replayRateUpDown.Enabled = csvMode;
        _loopReplayCheckBox.Enabled = csvMode;
        _serialPortComboBox.Enabled = !csvMode;
        _refreshPortsButton.Enabled = !csvMode;
        _baudRateComboBox.Enabled = !csvMode;
    }

    private void SetSourceConfigurationEnabled(bool enabled)
    {
        _csvModeRadioButton.Enabled = enabled;
        _serialModeRadioButton.Enabled = enabled;
        if (enabled)
        {
            UpdateSourceModeEnablement();
        }
        else
        {
            _csvPathTextBox.Enabled = false;
            _browseCsvButton.Enabled = false;
            _replayRateUpDown.Enabled = false;
            _loopReplayCheckBox.Enabled = false;
            _serialPortComboBox.Enabled = false;
            _refreshPortsButton.Enabled = false;
            _baudRateComboBox.Enabled = false;
        }
    }

    private void PersistPanelPreferences()
    {
        (ChartAxisVisibility gyro, ChartAxisVisibility accel, ChartAxisVisibility mag,
            bool overlayRaw, double windowSeconds) = _chartsPanel.SnapshotPreferences();
        (DisplayQuantity primary, DisplayQuantity secondary, bool primaryFiltered, bool secondaryFiltered) =
            _cubeViewsPanel.SnapshotPreferences();

        _settingsService.Update(s => s with
        {
            GyroscopeAxes = gyro,
            AccelerometerAxes = accel,
            MagnetometerAxes = mag,
            OverlayRawOnCharts = overlayRaw,
            ChartWindowSeconds = windowSeconds,
            PrimaryCubeQuantity = primary,
            SecondaryCubeQuantity = secondary,
            PrimaryCubeUsesFiltered = primaryFiltered,
            SecondaryCubeUsesFiltered = secondaryFiltered,
            ShowEnvironmentGrid = _environmentPanel.ShowGrid,
        });
    }

    private void OnUiThread(Action action)
    {
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private sealed record EstimatorChoice(OrientationEstimatorKind Kind, string Label)
    {
        public override string ToString() => Label;
    }
}
