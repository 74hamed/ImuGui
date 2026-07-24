namespace ImuGui.Core.Filtering;

/// <summary>How a filter treats its runtime state when new tuning parameters are applied.</summary>
public enum RetuneBehavior
{
    /// <summary>
    /// Restart estimation from the new configuration's initial state (X₀, P₀).
    /// The output will re-converge from scratch.
    /// </summary>
    ResetState,

    /// <summary>
    /// Keep the current estimate and covariance and only change Q/R. The output stays
    /// continuous; the new tuning takes effect gradually.
    /// </summary>
    PreserveState,
}
