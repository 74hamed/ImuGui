using System.Globalization;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using Orientation = ImuGui.Core.Models.Orientation;

namespace ImuGui.App.Views;

/// <summary>
/// Numeric readouts for every channel plus roll/pitch/yaw and temperature, consistently
/// formatted, switching between raw and filtered per the global toggle.
/// </summary>
public sealed class ReadoutsPanel : UserControl
{
    private const string NoValue = "—";

    private readonly Label[] _gyroValueLabels;
    private readonly Label[] _accelValueLabels;
    private readonly Label[] _magValueLabels;
    private readonly Label _rollValueLabel;
    private readonly Label _pitchValueLabel;
    private readonly Label _yawValueLabel;
    private readonly Label _temperatureValueLabel;
    private readonly Label _dataVariantLabel;

    public ReadoutsPanel()
    {
        Dock = DockStyle.Fill;
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(8),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _dataVariantLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font, FontStyle.Bold),
            Height = 24,
        };
        grid.Controls.Add(_dataVariantLabel, 0, 0);
        grid.SetColumnSpan(_dataVariantLabel, 2);

        grid.Controls.Add(CreateVectorGroup("Gyroscope (°/s)", out _gyroValueLabels), 0, 1);
        grid.Controls.Add(CreateVectorGroup("Accelerometer (g)", out _accelValueLabels), 1, 1);
        grid.Controls.Add(CreateVectorGroup("Magnetometer (µT)", out _magValueLabels), 0, 2);
        grid.Controls.Add(
            CreateOrientationGroup(
                out _rollValueLabel, out _pitchValueLabel, out _yawValueLabel, out _temperatureValueLabel),
            1, 2);

        Controls.Add(grid);
    }

    /// <summary>Updates every readout from the frame (dashes when no data yet).</summary>
    public void RenderFrame(ProcessedFrame? frame, bool useFiltered)
    {
        _dataVariantLabel.Text = useFiltered ? "Showing: filtered data" : "Showing: raw data";

        if (frame is null)
        {
            foreach (Label label in AllValueLabels())
            {
                label.Text = NoValue;
            }

            return;
        }

        SensorSample sample = useFiltered ? frame.FilteredSample : frame.RawSample;
        Orientation orientation = useFiltered ? frame.FilteredOrientation : frame.RawOrientation;

        SetVector(_gyroValueLabels, sample.Gyroscope, "F2");
        SetVector(_accelValueLabels, sample.Accelerometer, "F3");
        SetVector(_magValueLabels, sample.Magnetometer, "F1");
        _rollValueLabel.Text = Format(orientation.RollDegrees, "F1") + " °";
        _pitchValueLabel.Text = Format(orientation.PitchDegrees, "F1") + " °";
        _yawValueLabel.Text = Format(orientation.YawDegrees, "F1") + " °";
        _temperatureValueLabel.Text = Format(sample.TemperatureCelsius, "F1") + " °C";
    }

    private static string Format(double value, string format) =>
        value.ToString(format, CultureInfo.CurrentCulture);

    private static void SetVector(Label[] labels, Vector3 vector, string format)
    {
        labels[0].Text = Format(vector.X, format);
        labels[1].Text = Format(vector.Y, format);
        labels[2].Text = Format(vector.Z, format);
    }

    private IEnumerable<Label> AllValueLabels()
    {
        foreach (Label label in _gyroValueLabels.Concat(_accelValueLabels).Concat(_magValueLabels))
        {
            yield return label;
        }

        yield return _rollValueLabel;
        yield return _pitchValueLabel;
        yield return _yawValueLabel;
        yield return _temperatureValueLabel;
    }

    private GroupBox CreateVectorGroup(string title, out Label[] valueLabels)
    {
        string[] axisNames = ["X", "Y", "Z"];
        valueLabels = new Label[3];
        return CreateGroup(title, axisNames, valueLabels);
    }

    private GroupBox CreateOrientationGroup(
        out Label rollLabel, out Label pitchLabel, out Label yawLabel, out Label temperatureLabel)
    {
        string[] names = ["Roll", "Pitch", "Yaw", "Temperature"];
        var labels = new Label[4];
        GroupBox group = CreateGroup("Orientation", names, labels);
        rollLabel = labels[0];
        pitchLabel = labels[1];
        yawLabel = labels[2];
        temperatureLabel = labels[3];
        return group;
    }

    private GroupBox CreateGroup(string title, string[] rowNames, Label[] valueLabels)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(10) };
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rowNames.Length,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

        var valueFont = new Font(FontFamily.GenericMonospace, 11f, FontStyle.Bold);
        for (int i = 0; i < rowNames.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowNames.Length));
            table.Controls.Add(
                new Label
                {
                    Text = rowNames[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                },
                0, i);
            valueLabels[i] = new Label
            {
                Text = NoValue,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = valueFont,
            };
            table.Controls.Add(valueLabels[i], 1, i);
        }

        group.Controls.Add(table);
        return group;
    }
}
