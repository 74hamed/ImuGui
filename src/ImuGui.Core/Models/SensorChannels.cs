namespace ImuGui.Core.Models;

/// <summary>Helpers for enumerating <see cref="SensorChannel"/> values.</summary>
public static class SensorChannels
{
    /// <summary>The number of scalar channels in a sample.</summary>
    public const int Count = 10;

    /// <summary>All channels in column order.</summary>
    public static IReadOnlyList<SensorChannel> All { get; } =
        (SensorChannel[])Enum.GetValues(typeof(SensorChannel));
}
