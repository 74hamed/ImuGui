using System.Collections.Concurrent;
using ImuGui.Core.Models;
using ImuGui.Core.Sources;

namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>
/// Subscribes to a sensor source and lets tests await sample counts and state transitions
/// with real-time timeouts (source events arrive on background threads).
/// </summary>
internal sealed class SourceProbe : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly ISensorSource _source;
    private readonly SemaphoreSlim _sampleSignal = new(0);
    private readonly ConcurrentQueue<SensorSample> _samples = new();
    private readonly ConcurrentQueue<SensorConnectionState> _stateTransitions = new();
    private readonly ConcurrentQueue<string> _errors = new();

    internal SourceProbe(ISensorSource source)
    {
        _source = source;
        source.SampleReceived += OnSample;
        source.ConnectionStateChanged += OnStateChanged;
        source.ErrorOccurred += OnError;
    }

    internal IReadOnlyCollection<SensorSample> Samples => _samples.ToArray();

    internal IReadOnlyCollection<SensorConnectionState> StateTransitions => _stateTransitions.ToArray();

    internal IReadOnlyCollection<string> Errors => _errors.ToArray();

    internal async Task WaitForSamplesAsync(int count, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        for (int i = 0; i < count; i++)
        {
            await _sampleSignal.WaitAsync(cts.Token);
        }
    }

    internal async Task WaitForStateAsync(SensorConnectionState state, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        while (!_stateTransitions.Contains(state))
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, cts.Token);
        }
    }

    public void Dispose()
    {
        _source.SampleReceived -= OnSample;
        _source.ConnectionStateChanged -= OnStateChanged;
        _source.ErrorOccurred -= OnError;
        _sampleSignal.Dispose();
    }

    private void OnSample(object? sender, SensorSampleEventArgs e)
    {
        _samples.Enqueue(e.Sample);
        _sampleSignal.Release();
    }

    private void OnStateChanged(object? sender, SensorConnectionStateChangedEventArgs e) =>
        _stateTransitions.Enqueue(e.NewState);

    private void OnError(object? sender, SensorSourceErrorEventArgs e) => _errors.Enqueue(e.Message);
}
