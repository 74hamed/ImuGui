using System.Diagnostics;

namespace ImuGui.Core.Abstractions;

/// <summary>The production <see cref="IClock"/> backed by <see cref="Stopwatch"/> and <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
public sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <inheritdoc />
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
}
