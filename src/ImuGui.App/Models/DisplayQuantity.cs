namespace ImuGui.App.Models;

/// <summary>The quantity a 3-D cube view visualizes (an enum, never a display-string match).</summary>
public enum DisplayQuantity
{
    /// <summary>Accelerometer vector, visualized as rotation angles (1 g ≙ 90°).</summary>
    Accelerometer,

    /// <summary>Magnetometer vector, visualized as rotation angles (scaled).</summary>
    Magnetometer,

    /// <summary>Gyroscope rates, visualized as rotation angles (1 °/s ≙ 1°).</summary>
    Gyroscope,

    /// <summary>The fused attitude (roll/pitch/yaw).</summary>
    Orientation,
}
