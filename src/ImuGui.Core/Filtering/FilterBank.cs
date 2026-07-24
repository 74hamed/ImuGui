using ImuGui.Core.Models;

namespace ImuGui.Core.Filtering;

/// <summary>
/// One filter per <see cref="SensorChannel"/>, managed as a keyed collection. Thread-safe:
/// the acquisition thread calls <see cref="Process"/> while the UI retunes or resets.
/// </summary>
public sealed class FilterBank
{
    private readonly object _sync = new();
    private readonly Dictionary<SensorChannel, IScalarFilter> _filters;

    /// <summary>Creates a bank with one filter per channel from the given factory.</summary>
    /// <param name="filterFactory">Creates a filter instance for a configuration.</param>
    /// <param name="initialConfig">The tuning applied to every channel initially.</param>
    public FilterBank(Func<FilterConfig, IScalarFilter> filterFactory, FilterConfig initialConfig)
    {
        ArgumentNullException.ThrowIfNull(filterFactory);
        ArgumentNullException.ThrowIfNull(initialConfig);
        initialConfig.Validate();

        CurrentConfig = initialConfig;
        _filters = SensorChannels.All.ToDictionary(channel => channel, _ => filterFactory(initialConfig));
    }

    /// <summary>The most recently applied bank-wide tuning.</summary>
    public FilterConfig CurrentConfig { get; private set; }

    /// <summary>Creates a bank of <see cref="KalmanScalarFilter"/> instances.</summary>
    /// <param name="config">Optional tuning; <see cref="FilterConfig.Default"/> when null.</param>
    public static FilterBank CreateKalman(FilterConfig? config = null) =>
        new(c => new KalmanScalarFilter(c), config ?? FilterConfig.Default);

    /// <summary>Filters every channel of a sample and returns the filtered sample.</summary>
    /// <param name="sample">The raw sample.</param>
    public SensorSample Process(SensorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_sync)
        {
            double[] filteredValues = new double[SensorChannels.Count];
            foreach (SensorChannel channel in SensorChannels.All)
            {
                filteredValues[(int)channel] = _filters[channel].Update(sample.GetChannelValue(channel));
            }

            return SensorSample.FromChannelValues(sample.Timestamp, filteredValues);
        }
    }

    /// <summary>Applies new tuning to every channel.</summary>
    /// <param name="config">The new parameters (validated).</param>
    /// <param name="behavior">Whether each filter resets or preserves its state.</param>
    public void RetuneAll(FilterConfig config, RetuneBehavior behavior)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        lock (_sync)
        {
            foreach (IScalarFilter filter in _filters.Values)
            {
                filter.Retune(config, behavior);
            }

            CurrentConfig = config;
        }
    }

    /// <summary>Restarts every channel's estimation from its initial state.</summary>
    public void ResetAll()
    {
        lock (_sync)
        {
            foreach (IScalarFilter filter in _filters.Values)
            {
                filter.Reset();
            }
        }
    }

    /// <summary>
    /// Returns the filter for one channel, for inspection and tests. The instance is not
    /// thread-safe; do not call <see cref="IScalarFilter.Update"/> on it while the bank is processing.
    /// </summary>
    /// <param name="channel">The channel.</param>
    public IScalarFilter GetFilter(SensorChannel channel)
    {
        lock (_sync)
        {
            return _filters[channel];
        }
    }
}
