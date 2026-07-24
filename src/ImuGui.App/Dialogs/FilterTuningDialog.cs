using System.Globalization;
using ImuGui.Core.Filtering;

namespace ImuGui.App.Dialogs;

/// <summary>
/// Edits the Kalman parameters (Q/R/P₀/X₀) applied to the whole filter bank. Input is
/// validated before the dialog closes — non-numeric or out-of-range values are rejected
/// with an inline explanation, never applied.
/// </summary>
public sealed class FilterTuningDialog : Form
{
    private readonly TextBox _processNoiseTextBox;
    private readonly TextBox _measurementNoiseTextBox;
    private readonly TextBox _initialCovarianceTextBox;
    private readonly TextBox _initialEstimateTextBox;
    private readonly RadioButton _resetStateRadioButton;
    private readonly RadioButton _preserveStateRadioButton;
    private readonly Label _validationErrorLabel;

    public FilterTuningDialog(FilterConfig currentConfig)
    {
        ArgumentNullException.ThrowIfNull(currentConfig);

        Text = "Filter Tuning";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 8,
            AutoSize = true,
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _processNoiseTextBox = AddParameterRow(grid, 0, "Process noise Q:", currentConfig.ProcessNoise);
        _measurementNoiseTextBox = AddParameterRow(grid, 1, "Measurement noise R:", currentConfig.MeasurementNoise);
        _initialCovarianceTextBox = AddParameterRow(grid, 2, "Initial covariance P₀:", currentConfig.InitialCovariance);
        _initialEstimateTextBox = AddParameterRow(grid, 3, "Initial estimate X₀:", currentConfig.InitialEstimate);

        _resetStateRadioButton = new RadioButton
        {
            Text = "Reset filter state (restart from X₀ / P₀)",
            Checked = true,
            AutoSize = true,
        };
        _preserveStateRadioButton = new RadioButton
        {
            Text = "Keep current state (retune smoothly)",
            AutoSize = true,
        };
        grid.Controls.Add(_resetStateRadioButton, 0, 4);
        grid.SetColumnSpan(_resetStateRadioButton, 2);
        grid.Controls.Add(_preserveStateRadioButton, 0, 5);
        grid.SetColumnSpan(_preserveStateRadioButton, 2);

        _validationErrorLabel = new Label
        {
            ForeColor = Color.Firebrick,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Padding = new Padding(0, 6, 0, 0),
        };
        grid.Controls.Add(_validationErrorLabel, 0, 6);
        grid.SetColumnSpan(_validationErrorLabel, 2);

        var applyButton = new Button { Text = "Apply", AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        applyButton.Click += (_, _) => OnApplyClicked();

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Bottom,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttonRow.Controls.Add(applyButton);
        buttonRow.Controls.Add(cancelButton);
        grid.Controls.Add(buttonRow, 0, 7);
        grid.SetColumnSpan(buttonRow, 2);

        Controls.Add(grid);
        AcceptButton = applyButton;
        CancelButton = cancelButton;
    }

    /// <summary>The validated parameters, set only when the dialog closes with OK.</summary>
    public FilterConfig? ResultConfig { get; private set; }

    /// <summary>Whether filters reset or preserve their runtime state.</summary>
    public RetuneBehavior ResultBehavior { get; private set; } = RetuneBehavior.ResetState;

    private static TextBox AddParameterRow(TableLayoutPanel grid, int row, string label, double value)
    {
        grid.Controls.Add(
            new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 0) }, 0, row);
        var textBox = new TextBox
        {
            Width = 140,
            Text = value.ToString("R", CultureInfo.InvariantCulture),
        };
        grid.Controls.Add(textBox, 1, row);
        return textBox;
    }

    private void OnApplyClicked()
    {
        if (!TryParseField(_processNoiseTextBox, "Q", out double processNoise)
            || !TryParseField(_measurementNoiseTextBox, "R", out double measurementNoise)
            || !TryParseField(_initialCovarianceTextBox, "P₀", out double initialCovariance)
            || !TryParseField(_initialEstimateTextBox, "X₀", out double initialEstimate))
        {
            return;
        }

        var config = new FilterConfig
        {
            ProcessNoise = processNoise,
            MeasurementNoise = measurementNoise,
            InitialCovariance = initialCovariance,
            InitialEstimate = initialEstimate,
        };

        try
        {
            config.Validate();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _validationErrorLabel.Text = ex.Message.Split('\n')[0];
            return;
        }

        ResultConfig = config;
        ResultBehavior = _preserveStateRadioButton.Checked
            ? RetuneBehavior.PreserveState
            : RetuneBehavior.ResetState;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryParseField(TextBox textBox, string parameterName, out double value)
    {
        if (double.TryParse(
            textBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        _validationErrorLabel.Text =
            $"{parameterName} must be a number (use '.' as the decimal separator), e.g. 0.001.";
        textBox.Focus();
        textBox.SelectAll();
        return false;
    }
}
