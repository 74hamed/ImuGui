namespace ImuGui.Core.Sources;

/// <summary>Configuration for <see cref="CsvReplaySensorSource"/>.</summary>
public sealed record CsvReplayOptions
{
    /// <summary>Path to the CSV file to replay.</summary>
    public required string FilePath { get; init; }

    /// <summary>Replay rate in samples per second. Must be positive.</summary>
    public double ReplayRateHz { get; init; } = 50;

    /// <summary>When true, replay restarts from the beginning after the last row.</summary>
    public bool Loop { get; init; } = true;
}
