using ImuGui.Core.Models;

namespace ImuGui.Core.Sources;

/// <summary>Carries one acquired <see cref="SensorSample"/>.</summary>
public sealed class SensorSampleEventArgs : EventArgs
{
    /// <summary>Creates the event args.</summary>
    /// <param name="sample">The acquired sample.</param>
    public SensorSampleEventArgs(SensorSample sample) =>
        Sample = sample ?? throw new ArgumentNullException(nameof(sample));

    /// <summary>The acquired sample.</summary>
    public SensorSample Sample { get; }
}
