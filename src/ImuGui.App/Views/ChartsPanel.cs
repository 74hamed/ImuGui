using ImuGui.App.Settings;
using ImuGui.Core.Collections;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using ScottPlot;
using ScottPlot.WinForms;
using Label = System.Windows.Forms.Label;

namespace ImuGui.App.Views;

/// <summary>
/// Three scrolling time-series charts (gyroscope, accelerometer, magnetometer), each with
/// X/Y/Z series, per-axis visibility toggles, and an "overlay raw" toggle.
/// <para>
/// History is held in fixed-capacity <see cref="RingBuffer{T}"/>s — old points are dropped,
/// memory is bounded, and the visible window scrolls by timestamp. Appending happens on the
/// acquisition thread; rendering happens on the UI render tick.
/// </para>
/// </summary>
public sealed class ChartsPanel : UserControl
{
    private const int PointCapacityPerChart = 4096;

    private readonly ChannelChart[] _charts;
    private readonly CheckBox _overlayRawCheckBox;
    private SensorPipeline? _pipeline;
    private double _windowSeconds = 10;

    public ChartsPanel()
    {
        Dock = DockStyle.Fill;

        _charts =
        [
            new ChannelChart("Gyroscope", "°/s", sample => sample.Gyroscope),
            new ChannelChart("Accelerometer", "g", sample => sample.Accelerometer),
            new ChannelChart("Magnetometer", "µT", sample => sample.Magnetometer),
        ];

        _overlayRawCheckBox = new CheckBox { Text = "Overlay raw signal", AutoSize = true };

        var windowLabel = new Label
        {
            Text = "Window (s):",
            AutoSize = true,
            Padding = new Padding(16, 4, 0, 0),
        };
        var windowUpDown = new NumericUpDown
        {
            Minimum = 2,
            Maximum = 120,
            Value = 10,
            Width = 64,
        };
        windowUpDown.ValueChanged += (_, _) => _windowSeconds = (double)windowUpDown.Value;
        _windowUpDown = windowUpDown;

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 6, 8, 2),
        };
        topBar.Controls.Add(_overlayRawCheckBox);
        topBar.Controls.Add(windowLabel);
        topBar.Controls.Add(windowUpDown);

        var chartsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = _charts.Length,
        };
        foreach (ChannelChart chart in _charts)
        {
            chartsTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _charts.Length));
            chartsTable.Controls.Add(chart.Container);
        }

        Controls.Add(chartsTable);
        Controls.Add(topBar);
    }

    private readonly NumericUpDown _windowUpDown;

    /// <summary>Subscribes to the pipeline and applies persisted chart preferences.</summary>
    public void Attach(SensorPipeline pipeline, UserSettings settings)
    {
        _pipeline = pipeline;
        pipeline.FrameProcessed += OnFrameProcessed;

        _overlayRawCheckBox.Checked = settings.OverlayRawOnCharts;
        _windowSeconds = Math.Clamp(settings.ChartWindowSeconds, 2, 120);
        _windowUpDown.Value = (decimal)_windowSeconds;
        _charts[0].ApplyVisibility(settings.GyroscopeAxes);
        _charts[1].ApplyVisibility(settings.AccelerometerAxes);
        _charts[2].ApplyVisibility(settings.MagnetometerAxes);
    }

    /// <summary>Renders all charts from their ring buffers (skipped while the tab is hidden).</summary>
    public void RenderTick(bool filteringEnabled)
    {
        if (!Visible)
        {
            return;
        }

        bool overlayRaw = _overlayRawCheckBox.Checked;
        foreach (ChannelChart chart in _charts)
        {
            chart.Render(_windowSeconds, filteringEnabled, overlayRaw);
        }
    }

    /// <summary>Current preferences, for persistence on exit.</summary>
    public (ChartAxisVisibility Gyro, ChartAxisVisibility Accel, ChartAxisVisibility Mag,
        bool OverlayRaw, double WindowSeconds) SnapshotPreferences() => (
        _charts[0].SnapshotVisibility(),
        _charts[1].SnapshotVisibility(),
        _charts[2].SnapshotVisibility(),
        _overlayRawCheckBox.Checked,
        _windowSeconds);

    protected override void Dispose(bool disposing)
    {
        if (disposing && _pipeline is not null)
        {
            _pipeline.FrameProcessed -= OnFrameProcessed;
        }

        base.Dispose(disposing);
    }

    private void OnFrameProcessed(object? sender, ProcessedFrameEventArgs e)
    {
        // Acquisition thread: cheap bounded appends only.
        foreach (ChannelChart chart in _charts)
        {
            chart.Append(e.Frame);
        }
    }

    private readonly record struct ChartPoint(double TimeSeconds, Vector3 Raw, Vector3 Filtered);

    private sealed class ChannelChart
    {
        private static readonly ScottPlot.Color[] AxisColors =
        [
            ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(214, 39, 40)),   // X red
            ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(44, 160, 44)),   // Y green
            ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(31, 119, 180)),  // Z blue
        ];

        private readonly RingBuffer<ChartPoint> _points = new(PointCapacityPerChart);
        private readonly object _pointsSync = new();
        private readonly Func<SensorSample, Vector3> _vectorSelector;
        private readonly FormsPlot _formsPlot;
        private readonly CheckBox[] _axisCheckBoxes;

        internal ChannelChart(string title, string unit, Func<SensorSample, Vector3> vectorSelector)
        {
            _vectorSelector = vectorSelector;

            _formsPlot = new FormsPlot { Dock = DockStyle.Fill };
            _formsPlot.Plot.Title($"{title} ({unit})");
            _formsPlot.Plot.Axes.Bottom.Label.Text = "time (s)";

            string[] axisNames = ["X", "Y", "Z"];
            _axisCheckBoxes = new CheckBox[3];
            var sideBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.TopDown,
                Width = 64,
                Padding = new Padding(4, 24, 0, 0),
            };
            for (int i = 0; i < 3; i++)
            {
                _axisCheckBoxes[i] = new CheckBox { Text = axisNames[i], Checked = true, AutoSize = true };
                sideBar.Controls.Add(_axisCheckBoxes[i]);
            }

            Container = new Panel { Dock = DockStyle.Fill };
            Container.Controls.Add(_formsPlot);
            Container.Controls.Add(sideBar);
        }

        internal Panel Container { get; }

        internal void Append(ProcessedFrame frame)
        {
            var point = new ChartPoint(
                frame.Timestamp.TotalSeconds,
                _vectorSelector(frame.RawSample),
                _vectorSelector(frame.FilteredSample));
            lock (_pointsSync)
            {
                _points.Add(point);
            }
        }

        internal void ApplyVisibility(ChartAxisVisibility visibility)
        {
            _axisCheckBoxes[0].Checked = visibility.X;
            _axisCheckBoxes[1].Checked = visibility.Y;
            _axisCheckBoxes[2].Checked = visibility.Z;
        }

        internal ChartAxisVisibility SnapshotVisibility() => new(
            _axisCheckBoxes[0].Checked, _axisCheckBoxes[1].Checked, _axisCheckBoxes[2].Checked);

        internal void Render(double windowSeconds, bool mainSeriesIsFiltered, bool overlayRaw)
        {
            ChartPoint[] points;
            lock (_pointsSync)
            {
                points = _points.ToArray();
            }

            Plot plot = _formsPlot.Plot;
            plot.Clear();

            if (points.Length >= 2)
            {
                double[] times = new double[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    times[i] = points[i].TimeSeconds;
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    if (!_axisCheckBoxes[axis].Checked)
                    {
                        continue;
                    }

                    double[] mainValues = ExtractAxis(points, axis, filtered: mainSeriesIsFiltered);
                    var mainSeries = plot.Add.Scatter(times, mainValues);
                    mainSeries.Color = AxisColors[axis];
                    mainSeries.LineWidth = 2;
                    mainSeries.MarkerSize = 0;

                    if (overlayRaw && mainSeriesIsFiltered)
                    {
                        double[] rawValues = ExtractAxis(points, axis, filtered: false);
                        var rawSeries = plot.Add.Scatter(times, rawValues);
                        rawSeries.Color = AxisColors[axis].WithAlpha(0.30);
                        rawSeries.LineWidth = 1;
                        rawSeries.MarkerSize = 0;
                    }
                }

                double latestTime = points[^1].TimeSeconds;
                plot.Axes.AutoScale();
                plot.Axes.SetLimitsX(latestTime - windowSeconds, latestTime);
            }

            _formsPlot.Refresh();
        }

        private static double[] ExtractAxis(ChartPoint[] points, int axis, bool filtered)
        {
            double[] values = new double[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 vector = filtered ? points[i].Filtered : points[i].Raw;
                values[i] = axis switch
                {
                    0 => vector.X,
                    1 => vector.Y,
                    _ => vector.Z,
                };
            }

            return values;
        }
    }
}
