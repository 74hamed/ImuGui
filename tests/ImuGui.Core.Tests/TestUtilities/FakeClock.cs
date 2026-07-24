using ImuGui.Core.Abstractions;

namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>
/// A deterministic virtual-time clock: DelayAsync completes immediately and advances
/// virtual time by the requested amount, so replay pacing and reconnect delays run
/// instantly and reproducibly in tests.
/// </summary>
internal sealed class FakeClock : IClock
{
    private long _elapsedTicks;

    public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref _elapsedTicks));

    public TimeSpan TotalDelayRequested { get; private set; }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (delay > TimeSpan.Zero)
        {
            Interlocked.Add(ref _elapsedTicks, delay.Ticks);
            TotalDelayRequested += delay;
        }

        return Task.CompletedTask;
    }

    public void Advance(TimeSpan by) => Interlocked.Add(ref _elapsedTicks, by.Ticks);
}
