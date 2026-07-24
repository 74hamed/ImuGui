using System.Diagnostics;
using ImuGui.App.Dialogs;
using ImuGui.App.Models;
using ImuGui.App.Presenters;
using ImuGui.App.Settings;
using ImuGui.App.Theming;
using ImuGui.App.Views;
using ImuGui.Core.Calibration;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;

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

    private MainPresenter? _presenter;
    private readonly EventHandler _themeChangedHandler;

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

    // Action-row controls
    private readonly Button _connectButton;
    private readonly ConnectionStatusIndicator _connectionStatusIndicator;
    private readonly CheckBox _filterEnabledCheckBox;
    private readonly Button _tuneFiltersButton;
    private readonly ComboBox _estimatorComboBox;
    private readonly Button _calibrateButton;
    private readonly CheckBox _calibrationEnabledCheckBox;
    private readonly Button _settingsButton;
    private readonly Button _themeToggleButton;

    // Navigation + content sections (Settings is reached via the header gear button)
    private const int SettingsSectionIndex = 4;
    private readonly Button[] _navigationButtons;
    private readonly Control[] _sections;
    private readonly Font _navigationFont;
    private readonly Font _navigationActiveFont;
    private int _activeSectionIndex;

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
        MinimumSize = new Size(1024, 720);
        Size = new Size(1280, 840);

        // ── Source-row controls ─────────────────────────────────────────────
        _csvModeRadioButton = new RadioButton { Text = "CSV replay", Checked = true, AutoSize = true };
        _serialModeRadioButton = new RadioButton { Text = "Serial (COM)", AutoSize = true };
        _csvPathTextBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
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

        // ── Action-row controls ─────────────────────────────────────────────
        _connectButton = new Button
        {
            Text = "Connect",
            AutoSize = true,
            Padding = new Padding(18, 3, 18, 3),
            Tag = ThemeManager.AccentButtonTag,
        };
        _connectionStatusIndicator = new ConnectionStatusIndicator
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        _filterEnabledCheckBox = new CheckBox { Text = "Use filtered data", Checked = true, AutoSize = true };
        _tuneFiltersButton = new Button { Text = "Tune filters…", AutoSize = true };
        _estimatorComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        _estimatorComboBox.Items.Add(new EstimatorChoice(
            OrientationEstimatorKind.MahonyQuaternion, "Mahony (quaternion)"));
        _estimatorComboBox.Items.Add(new EstimatorChoice(
            OrientationEstimatorKind.EulerComplementary, "Complementary (Euler)"));
        _estimatorComboBox.Items.Add(new EstimatorChoice(
            OrientationEstimatorKind.KalmanEuler, "Kalman (Euler)"));
        _estimatorComboBox.SelectedIndex = 0;
        _calibrateButton = new Button { Text = "Calibrate…", AutoSize = true };
        _calibrationEnabledCheckBox = new CheckBox { Text = "Apply calibration", Checked = true, AutoSize = true };
        _settingsButton = new Button { Text = "⚙  Settings", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        _themeToggleButton = new Button { AutoSize = true, Padding = new Padding(8, 2, 8, 2) };

        // ── Navigation + sections ───────────────────────────────────────────
        _navigationFont = new Font(Font.FontFamily, 9.75f, FontStyle.Regular);
        _navigationActiveFont = new Font(Font.FontFamily, 9.75f, FontStyle.Bold);
        string[] sectionTitles = ["Dashboard", "Charts", "3D Views", "Environment"];
        _sections =
        [
            BuildDashboardSection(),
            _chartsPanel,
            _cubeViewsPanel,
            _environmentPanel,
            BuildSettingsSection(), // index 4 — opened via the header gear button
        ];
        _navigationButtons = new Button[sectionTitles.Length];
        for (int i = 0; i < sectionTitles.Length; i++)
        {
            int sectionIndex = i;
            _navigationButtons[i] = new Button
            {
                Text = sectionTitles[i],
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 6, 0),
                Font = _navigationFont,
            };
            _navigationButtons[i].Click += (_, _) => SelectSection(sectionIndex);
        }

        BuildLayout();
        WireLocalEvents();
        SelectSection(0);

        _themeChangedHandler = (_, _) => ApplyThemeToEverything();
        ThemeManager.ThemeChanged += _themeChangedHandler;
        ThemeManager.SetTheme(_settingsService.Current.UseDarkTheme ? AppTheme.Dark : AppTheme.Light);
        ApplyThemeToEverything();

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
        _chartsPanel.ApplyChartTheme(ThemeManager.Current);
        _cubeViewsPanel.Initialize(settings);
        _environmentPanel.ShowGrid = settings.ShowEnvironmentGrid;

        UpdateSourceModeEnablement();
        _renderTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _renderTimer.Stop();
        ThemeManager.ThemeChanged -= _themeChangedHandler;

        if (_presenter is { } presenter)
        {
            PersistPanelPreferences();
            presenter.SaveSettings();
            presenter.ShutDown(); // Acquisition stops before the window goes away.
        }

        base.OnFormClosing(e);
    }

    // ───────────────────────────── Layout ─────────────────────────────────────

    private void BuildLayout()
    {
        // Minimal header that fits at any window width/DPI: connect + real status +
        // the everyday raw/filtered toggle + theme. Everything else lives in Settings.
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 7,
            Padding = new Padding(10, 8, 10, 6),
        };
        AddAutoRow(header);
        AddCell(header, _connectButton, 0, autoSize: true);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(_connectionStatusIndicator, 1, 0);
        AddCell(header, CreateSeparator(), 2, autoSize: true);
        AddCell(header, _filterEnabledCheckBox, 3, autoSize: true);
        AddCell(header, CreateSeparator(), 4, autoSize: true);
        AddCell(header, _themeToggleButton, 5, autoSize: true);
        AddCell(header, _settingsButton, 6, autoSize: true);

        // Flat navigation bar (replaces the system tab strip, which cannot be themed).
        var navigationBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 2, 10, 0),
        };
        foreach (Button navigationButton in _navigationButtons)
        {
            navigationBar.Controls.Add(navigationButton);
        }

        var navigationHairline = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            Tag = ThemeManager.SeparatorTag,
        };

        var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        foreach (Control section in _sections)
        {
            section.Dock = DockStyle.Fill;
            section.Visible = false;
            contentHost.Controls.Add(section);
        }

        var statusStrip = new StatusStrip { SizingGrip = false };
        statusStrip.Items.Add(_messageStatusLabel);
        statusStrip.Items.Add(_sampleRateStatusLabel);
        statusStrip.Items.Add(_frameCountStatusLabel);

        Controls.Add(contentHost);
        Controls.Add(navigationHairline);
        Controls.Add(navigationBar);
        Controls.Add(header);
        Controls.Add(statusStrip);
    }

    private Control BuildDashboardSection()
    {
        var dashboard = new TableLayoutPanel { ColumnCount = 2, RowCount = 1 };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _instrumentsPanel.Dock = DockStyle.Fill;
        _readoutsPanel.Dock = DockStyle.Fill;
        dashboard.Controls.Add(_instrumentsPanel, 0, 0);
        dashboard.Controls.Add(_readoutsPanel, 1, 0);
        return dashboard;
    }

    private Control BuildSettingsSection()
    {
        // Auto-size all the way down (no dock/auto-size circularity): fixed-width
        // inputs inside auto-size tables inside auto-size group boxes.
        _csvPathTextBox.Anchor = AnchorStyles.Left;
        _csvPathTextBox.Width = 340;

        var sourceTable = CreateSettingsTable();
        AddSettingsHeaderRow(sourceTable, 0, _csvModeRadioButton);
        AddSettingsRow(sourceTable, 1, "File", InlineFlow(_csvPathTextBox, _browseCsvButton));
        AddSettingsRow(sourceTable, 2, "Rate (Hz)", InlineFlow(_replayRateUpDown, _loopReplayCheckBox));
        AddSettingsHeaderRow(sourceTable, 3, _serialModeRadioButton);
        AddSettingsRow(sourceTable, 4, "Port", InlineFlow(_serialPortComboBox, _refreshPortsButton));
        AddSettingsRow(sourceTable, 5, "Baud", InlineFlow(_baudRateComboBox));

        var processingTable = CreateSettingsTable();
        AddSettingsRow(processingTable, 0, "Kalman filter", InlineFlow(
            _tuneFiltersButton,
            CreateInlineLabel("Raw/filtered display toggle is in the header.")));
        AddSettingsRow(processingTable, 1, "Fusion", InlineFlow(_estimatorComboBox));
        AddSettingsRow(processingTable, 2, "Calibration", InlineFlow(
            _calibrateButton, _calibrationEnabledCheckBox));

        var settingsColumn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10),
        };
        settingsColumn.Controls.Add(WrapInGroup("Data source", sourceTable));
        settingsColumn.Controls.Add(WrapInGroup("Processing", processingTable));
        return settingsColumn;
    }

    private static TableLayoutPanel CreateSettingsTable() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2,
        ColumnStyles = { new ColumnStyle(SizeType.Absolute, 110), new ColumnStyle(SizeType.AutoSize) },
    };

    private static void AddSettingsHeaderRow(TableLayoutPanel table, int row, Control header)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Margin = new Padding(3, row == 0 ? 4 : 14, 3, 4);
        table.Controls.Add(header, 0, row);
        table.SetColumnSpan(header, 2);
    }

    private void AddSettingsRow(TableLayoutPanel table, int row, string labelText, Control content)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Label label = CreateInlineLabel(labelText);
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(26, 8, 8, 4);
        table.Controls.Add(label, 0, row);
        content.Anchor = AnchorStyles.Left;
        table.Controls.Add(content, 1, row);
    }

    private static FlowLayoutPanel InlineFlow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0),
        };
        foreach (Control control in controls)
        {
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(3, 4, 8, 4);
            flow.Controls.Add(control);
        }

        return flow;
    }

    private static GroupBox WrapInGroup(string title, Control content)
    {
        var group = new GroupBox
        {
            Text = title,
            // Dock=Top auto-size child + a minimum width sizes deterministically;
            // Dock=Fill inside an auto-size container collapses (the tuning-dialog bug).
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(640, 0),
            Padding = new Padding(14, 6, 14, 12),
            Margin = new Padding(0, 0, 0, 14),
        };
        content.Dock = DockStyle.Top;
        group.Controls.Add(content);
        return group;
    }

    private static void AddAutoRow(TableLayoutPanel table) =>
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

    private static void AddCell(TableLayoutPanel table, Control control, int column, bool autoSize)
    {
        if (autoSize)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        control.Anchor = control.Anchor == (AnchorStyles.Left | AnchorStyles.Right)
            ? control.Anchor
            : AnchorStyles.Left;
        control.Margin = new Padding(4, 4, 4, 4);
        table.Controls.Add(control, column, 0);
    }

    private static Label CreateInlineLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Tag = ThemeManager.SecondaryTextTag,
    };

    private static Panel CreateSeparator() => new()
    {
        Width = 1,
        Height = 26,
        Tag = ThemeManager.SeparatorTag,
        Margin = new Padding(10, 4, 10, 4),
        Anchor = AnchorStyles.None,
    };

    // ───────────────────────────── Theming ────────────────────────────────────

    private void ApplyThemeToEverything()
    {
        AppTheme theme = ThemeManager.Current;
        ThemeManager.ApplyToWindow(this);
        _chartsPanel.ApplyChartTheme(theme);
        StyleNavigationButtons();
        _themeToggleButton.Text = theme.IsDark ? "☀  Light" : "🌙  Dark";
        Invalidate(true);
    }

    private void StyleNavigationButtons()
    {
        AppTheme theme = ThemeManager.Current;
        for (int i = 0; i < _navigationButtons.Length; i++)
        {
            Button button = _navigationButtons[i];
            bool active = i == _activeSectionIndex;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = active ? _navigationActiveFont : _navigationFont;
            button.BackColor = active ? theme.SurfaceBackground : theme.WindowBackground;
            button.ForeColor = active ? theme.Accent : theme.SecondaryText;
            button.FlatAppearance.MouseOverBackColor = theme.SurfaceBackground;
        }

        // The header gear button doubles as the Settings "tab": accent while open.
        bool settingsActive = _activeSectionIndex == SettingsSectionIndex;
        _settingsButton.ForeColor = settingsActive ? theme.Accent : theme.PrimaryText;
        _settingsButton.FlatAppearance.BorderColor = settingsActive ? theme.Accent : theme.Border;
    }

    // ───────────────────────────── Behavior ───────────────────────────────────

    private void SelectSection(int index)
    {
        _activeSectionIndex = index;
        for (int i = 0; i < _sections.Length; i++)
        {
            _sections[i].Visible = i == index;
        }

        StyleNavigationButtons();
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
        _settingsButton.Click += (_, _) => SelectSection(SettingsSectionIndex);
        _themeToggleButton.Click += (_, _) => OnThemeToggleClicked();
        _environmentPanel.GridVisibilityChanged += (_, _) =>
            _settingsService.Update(s => s with { ShowEnvironmentGrid = _environmentPanel.ShowGrid });
    }

    private void OnThemeToggleClicked()
    {
        AppTheme next = ThemeManager.Current.IsDark ? AppTheme.Light : AppTheme.Dark;
        ThemeManager.SetTheme(next);
        _settingsService.Update(s => s with { UseDarkTheme = next.IsDark });
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
        double measuredHz = (frameCount - _lastRateFrameCount) / _rateStopwatch.Elapsed.TotalSeconds;
        _lastRateFrameCount = frameCount;
        _rateStopwatch.Restart();
        _sampleRateStatusLabel.Text = $"{measuredHz:F1} Hz";
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
