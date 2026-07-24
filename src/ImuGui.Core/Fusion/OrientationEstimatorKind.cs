namespace ImuGui.Core.Fusion;

/// <summary>The selectable fusion strategies.</summary>
public enum OrientationEstimatorKind
{
    /// <summary>Quaternion-based Mahony MARG filter (default; no gimbal lock).</summary>
    MahonyQuaternion,

    /// <summary>Euler-angle complementary filter (simpler; gimbal ambiguity near ±90° pitch).</summary>
    EulerComplementary,
}
