using ImuGui.App.Models;
using ImuGui.Core.Models;

namespace ImuGui.App.Presenters;

/// <summary>
/// The passive main view consumed by <see cref="MainPresenter"/>. Implementations own all
/// UI-thread marshalling — presenter calls may arrive from background threads.
/// </summary>
public interface IMainView
{
    /// <summary>The selected source kind.</summary>
    SourceMode SelectedSourceMode { get; set; }

    /// <summary>The CSV file path for replay mode.</summary>
    string CsvFilePath { get; set; }

    /// <summary>The CSV replay rate in Hz.</summary>
    double ReplayRateHz { get; set; }

    /// <summary>Whether CSV replay loops.</summary>
    bool LoopReplay { get; set; }

    /// <summary>The selected COM port, or null when none is selected.</summary>
    string? SelectedSerialPort { get; set; }

    /// <summary>The selected baud rate.</summary>
    int SelectedBaudRate { get; set; }

    /// <summary>The global raw/filtered toggle state shown in the UI.</summary>
    bool FilteringEnabled { get; set; }

    /// <summary>The calibration on/off toggle state shown in the UI.</summary>
    bool CalibrationEnabled { get; set; }

    /// <summary>The selected fusion strategy.</summary>
    Core.Fusion.OrientationEstimatorKind SelectedEstimatorKind { get; set; }

    /// <summary>Populates the COM port picker.</summary>
    /// <param name="ports">Available port names.</param>
    void SetAvailableSerialPorts(IReadOnlyList<string> ports);

    /// <summary>Reflects the real connection state (color + text). Never fakes "connected".</summary>
    /// <param name="state">The source's current state.</param>
    /// <param name="sourceDisplayName">The source's display name.</param>
    void SetConnectionState(SensorConnectionState state, string sourceDisplayName);

    /// <summary>Shows a non-blocking status message (e.g. reconnect attempts) in the status bar.</summary>
    /// <param name="message">The message.</param>
    void SetStatusMessage(string message);

    /// <summary>Shows a modal error with an actionable message.</summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The user-presentable message.</param>
    void ShowError(string title, string message);

    /// <summary>Shows a modal warning.</summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The user-presentable message.</param>
    void ShowWarning(string title, string message);
}
