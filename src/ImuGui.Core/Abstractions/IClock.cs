namespace ImuGui.Core.Abstractions;

/// <summary>
/// An injectable monotonic clock. All time measurement in ImuGui (sample timestamps,
/// per-sample dt, replay pacing) flows through this abstraction so that time-dependent
/// logic is deterministic under test.
/// </summary>
public interface IClock
{
    /// <summary>Monotonic elapsed time since the clock was created.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>Waits for the given duration (completes immediately for non-positive values).</summary>
    /// <param name="delay">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
