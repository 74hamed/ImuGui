namespace ImuGui.Core.Fusion;

/// <summary>Creates estimator instances from a <see cref="OrientationEstimatorKind"/>.</summary>
public static class OrientationEstimatorFactory
{
    /// <summary>Creates a fresh estimator of the requested kind with default tuning.</summary>
    /// <param name="kind">The strategy to create.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not a known value.</exception>
    public static IOrientationEstimator Create(OrientationEstimatorKind kind) => kind switch
    {
        OrientationEstimatorKind.MahonyQuaternion => new MahonyOrientationEstimator(),
        OrientationEstimatorKind.EulerComplementary => new ComplementaryOrientationEstimator(),
        OrientationEstimatorKind.KalmanEuler => new KalmanOrientationEstimator(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown estimator kind."),
    };
}
