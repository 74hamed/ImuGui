using System.Globalization;
using ImuGui.Core.Calibration;
using ImuGui.Core.Models;

namespace ImuGui.App.Dialogs;

/// <summary>
/// The calibration workflow: capture stationary gyro bias, six-position accelerometer
/// bias/scale, and magnetometer hard/soft-iron, review the computed results, then apply
/// them to the live stream and persist. Samples arrive via <see cref="SubmitSample"/>
/// (raw, pre-pipeline, from any thread) while a capture is armed.
/// </summary>
public sealed class CalibrationDialog : Form
{
    private static readonly Dictionary<AccelerometerCalibrationFace, string> FaceInstructions = new()
    {
        [AccelerometerCalibrationFace.ZDown] = "Rest the device flat on the table (normal position).",
        [AccelerometerCalibrationFace.ZUp] = "Rest the device upside down.",
        [AccelerometerCalibrationFace.XDown] = "Stand the device on its nose (X axis down).",
        [AccelerometerCalibrationFace.XUp] = "Stand the device on its tail (X axis up).",
        [AccelerometerCalibrationFace.YDown] = "Rest the device on its right side (Y axis down).",
        [AccelerometerCalibrationFace.YUp] = "Rest the device on its left side (Y axis up).",
    };

    private readonly ICalibrationService _calibrationService;

    private readonly object _captureSync = new();
    private readonly GyroscopeBiasCalibrator _gyroCalibrator = new();
    private readonly AccelerometerSixPositionCalibrator _accelCalibrator = new();
    private readonly MagnetometerCalibrator _magCalibrator = new();
    private CaptureTarget _activeCapture = CaptureTarget.None;
    private AccelerometerCalibrationFace _selectedFace = AccelerometerCalibrationFace.ZDown;

    private Vector3? _pendingGyroBias;
    private AccelerometerCalibrationResult? _pendingAccelResult;
    private MagnetometerCalibrationResult? _pendingMagResult;

    private readonly System.Windows.Forms.Timer _progressTimer;
    private readonly Button _gyroCaptureButton;
    private readonly Label _gyroProgressLabel;
    private readonly Label _gyroResultLabel;
    private readonly ComboBox _faceComboBox;
    private readonly Label _faceInstructionLabel;
    private readonly Button _accelCaptureButton;
    private readonly Label _accelProgressLabel;
    private readonly Label _accelResultLabel;
    private readonly Button _magCaptureButton;
    private readonly Label _magProgressLabel;
    private readonly Label _magResultLabel;
    private readonly Label _summaryLabel;

    private enum CaptureTarget
    {
        None,
        Gyroscope,
        Accelerometer,
        Magnetometer,
    }

    public CalibrationDialog(ICalibrationService calibrationService)
    {
        _calibrationService = calibrationService ?? throw new ArgumentNullException(nameof(calibrationService));

        Text = "Sensor Calibration";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 520);
        Size = new Size(560, 520);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // ── Gyroscope tab ───────────────────────────────────────────────────
        _gyroCaptureButton = new Button { Text = "Start capture", AutoSize = true };
        _gyroProgressLabel = new Label { AutoSize = true, Text = "0 samples" };
        _gyroResultLabel = new Label { AutoSize = true, Text = "No result yet.", Font = new Font(FontFamily.GenericMonospace, 9f) };
        var gyroComputeButton = new Button { Text = "Compute bias", AutoSize = true };
        gyroComputeButton.Click += (_, _) => ComputeGyro();
        _gyroCaptureButton.Click += (_, _) => ToggleCapture(CaptureTarget.Gyroscope, _gyroCaptureButton);
        tabs.TabPages.Add(BuildTab(
            "Gyroscope",
            "Leave the device completely still on a stable surface, then capture a few seconds of data.",
            [_gyroCaptureButton, gyroComputeButton],
            [_gyroProgressLabel, _gyroResultLabel]));

        // ── Accelerometer tab ───────────────────────────────────────────────
        _faceComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        foreach (AccelerometerCalibrationFace face in Enum.GetValues<AccelerometerCalibrationFace>())
        {
            _faceComboBox.Items.Add(face);
        }

        _faceComboBox.SelectedItem = _selectedFace;
        _faceInstructionLabel = new Label { AutoSize = true, Text = FaceInstructions[_selectedFace] };
        _faceComboBox.SelectedIndexChanged += (_, _) =>
        {
            lock (_captureSync)
            {
                _selectedFace = (AccelerometerCalibrationFace)_faceComboBox.SelectedItem!;
            }

            _faceInstructionLabel.Text = FaceInstructions[_selectedFace];
        };
        _accelCaptureButton = new Button { Text = "Start capture", AutoSize = true };
        _accelCaptureButton.Click += (_, _) => ToggleCapture(CaptureTarget.Accelerometer, _accelCaptureButton);
        _accelProgressLabel = new Label { AutoSize = true, Text = string.Empty, Font = new Font(FontFamily.GenericMonospace, 9f) };
        _accelResultLabel = new Label { AutoSize = true, Text = "No result yet.", Font = new Font(FontFamily.GenericMonospace, 9f) };
        var accelComputeButton = new Button { Text = "Compute bias && scale", AutoSize = true };
        accelComputeButton.Click += (_, _) => ComputeAccel();
        tabs.TabPages.Add(BuildTab(
            "Accelerometer",
            "Rest the device on each of its six faces and capture each position (six-position method).",
            [_faceComboBox, _accelCaptureButton, accelComputeButton],
            [_faceInstructionLabel, _accelProgressLabel, _accelResultLabel]));

        // ── Magnetometer tab ────────────────────────────────────────────────
        _magCaptureButton = new Button { Text = "Start capture", AutoSize = true };
        _magCaptureButton.Click += (_, _) => ToggleCapture(CaptureTarget.Magnetometer, _magCaptureButton);
        _magProgressLabel = new Label { AutoSize = true, Text = "0 samples", Font = new Font(FontFamily.GenericMonospace, 9f) };
        _magResultLabel = new Label { AutoSize = true, Text = "No result yet.", Font = new Font(FontFamily.GenericMonospace, 9f) };
        var magComputeButton = new Button { Text = "Compute offsets", AutoSize = true };
        magComputeButton.Click += (_, _) => ComputeMag();
        tabs.TabPages.Add(BuildTab(
            "Magnetometer",
            "Sweep the device slowly through a 3-D figure-eight, rotating through all orientations.",
            [_magCaptureButton, magComputeButton],
            [_magProgressLabel, _magResultLabel]));

        // ── Bottom bar ──────────────────────────────────────────────────────
        _summaryLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Text = SummaryText(),
        };
        var applyButton = new Button { Text = "Apply && save profile", AutoSize = true };
        applyButton.Click += (_, _) => ApplyPendingResults();
        var resetButton = new Button { Text = "Reset to identity", AutoSize = true };
        resetButton.Click += (_, _) => ResetProfile();
        var closeButton = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        buttonBar.Controls.Add(closeButton);
        buttonBar.Controls.Add(resetButton);
        buttonBar.Controls.Add(applyButton);

        Controls.Add(tabs);
        Controls.Add(_summaryLabel);
        Controls.Add(buttonBar);
        CancelButton = closeButton;

        _progressTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _progressTimer.Tick += (_, _) => RefreshProgressLabels();
        _progressTimer.Start();

        Theming.ThemeManager.ApplyToWindow(this);
    }

    /// <summary>Feeds one raw sample; routed to the armed calibrator. Safe from any thread.</summary>
    public void SubmitSample(SensorSample sample)
    {
        lock (_captureSync)
        {
            switch (_activeCapture)
            {
                case CaptureTarget.Gyroscope:
                    _gyroCalibrator.AddSample(sample);
                    break;
                case CaptureTarget.Accelerometer:
                    _accelCalibrator.AddSample(_selectedFace, sample);
                    break;
                case CaptureTarget.Magnetometer:
                    _magCalibrator.AddSample(sample);
                    break;
                case CaptureTarget.None:
                default:
                    break;
            }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        lock (_captureSync)
        {
            _activeCapture = CaptureTarget.None;
        }

        _progressTimer.Stop();
        _progressTimer.Dispose();
        base.OnFormClosed(e);
    }

    private static TabPage BuildTab(string title, string instructions, Control[] actions, Control[] status)
    {
        var page = new TabPage(title);
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
        };
        layout.Controls.Add(new Label
        {
            Text = instructions,
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Padding = new Padding(0, 0, 0, 10),
        });

        var actionRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        foreach (Control control in actions)
        {
            actionRow.Controls.Add(control);
        }

        layout.Controls.Add(actionRow);
        foreach (Control control in status)
        {
            control.Padding = new Padding(0, 8, 0, 0);
            layout.Controls.Add(control);
        }

        page.Controls.Add(layout);
        return page;
    }

    private void ToggleCapture(CaptureTarget target, Button button)
    {
        lock (_captureSync)
        {
            if (_activeCapture == target)
            {
                _activeCapture = CaptureTarget.None;
            }
            else
            {
                _activeCapture = target;
            }
        }

        bool capturing = _activeCapture == target;
        _gyroCaptureButton.Text = "Start capture";
        _accelCaptureButton.Text = "Start capture";
        _magCaptureButton.Text = "Start capture";
        if (capturing)
        {
            button.Text = "Stop capture";
        }
    }

    private void ComputeGyro()
    {
        try
        {
            lock (_captureSync)
            {
                _pendingGyroBias = _gyroCalibrator.ComputeBias();
            }

            _gyroResultLabel.Text = $"Bias: {_pendingGyroBias} °/s";
            RefreshSummary();
        }
        catch (CalibrationException ex)
        {
            MessageBox.Show(this, ex.Message, "Gyroscope calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ComputeAccel()
    {
        try
        {
            lock (_captureSync)
            {
                _pendingAccelResult = _accelCalibrator.ComputeResult();
            }

            _accelResultLabel.Text =
                $"Bias:  {_pendingAccelResult.Bias} g\nScale: {_pendingAccelResult.Scale}";
            RefreshSummary();
        }
        catch (CalibrationException ex)
        {
            MessageBox.Show(this, ex.Message, "Accelerometer calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ComputeMag()
    {
        try
        {
            lock (_captureSync)
            {
                _pendingMagResult = _magCalibrator.ComputeResult();
            }

            _magResultLabel.Text =
                $"Hard-iron: {_pendingMagResult.HardIronOffset}\nSoft-iron: {_pendingMagResult.SoftIronScale}";
            RefreshSummary();
        }
        catch (CalibrationException ex)
        {
            MessageBox.Show(this, ex.Message, "Magnetometer calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ApplyPendingResults()
    {
        if (_pendingGyroBias is null && _pendingAccelResult is null && _pendingMagResult is null)
        {
            MessageBox.Show(
                this, "Nothing to apply yet — compute at least one calibration result first.",
                "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CalibrationProfile profile = _calibrationService.CurrentProfile with
        {
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        if (_pendingGyroBias is { } gyroBias)
        {
            profile = profile with { GyroscopeBias = gyroBias };
        }

        if (_pendingAccelResult is { } accel)
        {
            profile = profile with { AccelerometerBias = accel.Bias, AccelerometerScale = accel.Scale };
        }

        if (_pendingMagResult is { } mag)
        {
            profile = profile with
            {
                MagnetometerHardIronOffset = mag.HardIronOffset,
                MagnetometerSoftIronScale = mag.SoftIronScale,
            };
        }

        try
        {
            _calibrationService.ApplyProfile(profile);
            _summaryLabel.Text = SummaryText() + "  (applied ✓)";
        }
        catch (CalibrationException ex)
        {
            // The profile is active in memory; persistence failed — tell the user honestly.
            MessageBox.Show(this, ex.Message, "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetProfile()
    {
        DialogResult confirmation = MessageBox.Show(
            this, "Discard the active calibration profile and revert to identity?",
            "Calibration", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _calibrationService.ResetToIdentity();
            _pendingGyroBias = null;
            _pendingAccelResult = null;
            _pendingMagResult = null;
            _summaryLabel.Text = SummaryText();
        }
        catch (CalibrationException ex)
        {
            MessageBox.Show(this, ex.Message, "Calibration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshProgressLabels()
    {
        lock (_captureSync)
        {
            _gyroProgressLabel.Text =
                $"{_gyroCalibrator.SampleCount} samples"
                + (_gyroCalibrator.HasEnoughSamples ? "  (enough ✓)" : $"  (need ≥ {GyroscopeBiasCalibrator.MinimumSampleCount})");

            var faceLines = Enum.GetValues<AccelerometerCalibrationFace>()
                .Select(face =>
                    $"{face,-6} {_accelCalibrator.SampleCountFor(face),4}"
                    + (_accelCalibrator.IsFaceCaptured(face) ? " ✓" : string.Empty));
            _accelProgressLabel.Text = string.Join('\n', faceLines);

            _magProgressLabel.Text = string.Create(
                CultureInfo.InvariantCulture,
                $"{_magCalibrator.SampleCount} samples\nmin {_magCalibrator.CurrentMinimum}\nmax {_magCalibrator.CurrentMaximum}");
        }
    }

    private void RefreshSummary() => _summaryLabel.Text = SummaryText();

    private string SummaryText()
    {
        var parts = new List<string>();
        parts.Add(_pendingGyroBias is null ? "gyro: –" : "gyro: ready");
        parts.Add(_pendingAccelResult is null ? "accel: –" : "accel: ready");
        parts.Add(_pendingMagResult is null ? "mag: –" : "mag: ready");
        string active = _calibrationService.CurrentProfile.IsIdentity ? "identity" : "custom profile";
        return $"Pending results — {string.Join(", ", parts)}   ·   Active profile: {active}";
    }
}
