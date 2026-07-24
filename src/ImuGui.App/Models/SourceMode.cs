namespace ImuGui.App.Models;

/// <summary>Which kind of sensor source the user selected.</summary>
public enum SourceMode
{
    /// <summary>Replay a recorded CSV file (the default/offline mode).</summary>
    CsvReplay,

    /// <summary>Read live from a serial (COM) port.</summary>
    Serial,
}
