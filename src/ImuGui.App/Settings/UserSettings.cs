using ImuGui.App.Models;
using ImuGui.Core.Filtering;
using ImuGui.Core.Fusion;

namespace ImuGui.App.Settings;

/// <summary>Per-axis chart visibility.</summary>
/// <param name="X">Show the X series.</param>
/// <param name="Y">Show the Y series.</param>
/// <param name="Z">Show the Z series.</param>
public sealed record ChartAxisVisibility(bool X = true, bool Y = true, bool Z = true);

/// <summary>
/// Everything persisted between runs, stored as JSON in %AppData%\ImuGui\settings.json
/// (the calibration profile lives in its own file).
/// </summary>
public sealed record UserSettings
{
    /// <summary>Whether the dark theme is active (the default).</summary>
    public bool UseDarkTheme { get; init; } = true;

    /// <summary>The selected source kind.</summary>
    public SourceMode SourceMode { get; init; } = SourceMode.CsvReplay;

    /// <summary>Path of the last replayed CSV file.</summary>
    public string CsvFilePath { get; init; } = string.Empty;

    /// <summary>CSV replay rate in Hz.</summary>
    public double ReplayRateHz { get; init; } = 50;

    /// <summary>Whether CSV replay loops.</summary>
    public bool LoopReplay { get; init; } = true;

    /// <summary>The last used COM port.</summary>
    public string SerialPortName { get; init; } = string.Empty;

    /// <summary>The last used baud rate.</summary>
    public int BaudRate { get; init; } = 115200;

    /// <summary>The global raw/filtered toggle.</summary>
    public bool FilteringEnabled { get; init; } = true;

    /// <summary>Kalman tuning applied to the filter bank.</summary>
    public FilterConfig FilterConfig { get; init; } = FilterConfig.Default;

    /// <summary>The selected fusion strategy.</summary>
    public OrientationEstimatorKind EstimatorKind { get; init; } = OrientationEstimatorKind.MahonyQuaternion;

    /// <summary>Whether the calibration profile is applied to the stream.</summary>
    public bool CalibrationEnabled { get; init; } = true;

    /// <summary>Visible time window of the scrolling charts, in seconds.</summary>
    public double ChartWindowSeconds { get; init; } = 10;

    /// <summary>Whether charts overlay the raw signal on the filtered one.</summary>
    public bool OverlayRawOnCharts { get; init; }

    /// <summary>Per-axis visibility of the gyroscope chart.</summary>
    public ChartAxisVisibility GyroscopeAxes { get; init; } = new();

    /// <summary>Per-axis visibility of the accelerometer chart.</summary>
    public ChartAxisVisibility AccelerometerAxes { get; init; } = new();

    /// <summary>Per-axis visibility of the magnetometer chart.</summary>
    public ChartAxisVisibility MagnetometerAxes { get; init; } = new();

    /// <summary>Whether the environment view draws its reference grid.</summary>
    public bool ShowEnvironmentGrid { get; init; } = true;

    /// <summary>The first cube view's quantity.</summary>
    public DisplayQuantity PrimaryCubeQuantity { get; init; } = DisplayQuantity.Accelerometer;

    /// <summary>The second cube view's quantity.</summary>
    public DisplayQuantity SecondaryCubeQuantity { get; init; } = DisplayQuantity.Orientation;

    /// <summary>The first cube view's raw/filtered toggle.</summary>
    public bool PrimaryCubeUsesFiltered { get; init; } = true;

    /// <summary>The second cube view's raw/filtered toggle.</summary>
    public bool SecondaryCubeUsesFiltered { get; init; } = true;
}
