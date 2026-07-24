namespace ImuGui.Core.Filtering;

/// <summary>A stateful filter over one scalar channel. Not thread-safe; callers serialize access.</summary>
public interface IScalarFilter
{
    /// <summary>The active tuning parameters.</summary>
    FilterConfig Config { get; }

    /// <summary>The most recent filtered estimate.</summary>
    double Value { get; }

    /// <summary>Feeds one raw measurement and returns the updated estimate.</summary>
    /// <param name="measurement">The raw measurement.</param>
    double Update(double measurement);

    /// <summary>Restarts estimation from the configuration's initial state.</summary>
    void Reset();

    /// <summary>Applies new tuning parameters.</summary>
    /// <param name="config">The new parameters (validated).</param>
    /// <param name="behavior">Whether to reset or preserve the runtime state.</param>
    void Retune(FilterConfig config, RetuneBehavior behavior);
}
