namespace ImuGui.Core.Calibration;

/// <summary>
/// The six rest positions of the accelerometer calibration routine, named for the body
/// axis pointing toward the ground. In each position the named axis should read ±1 g
/// (−1 g for a "down" face, +1 g for an "up" face).
/// </summary>
public enum AccelerometerCalibrationFace
{
    /// <summary>Body X axis points down (nose down).</summary>
    XDown,

    /// <summary>Body X axis points up (nose up).</summary>
    XUp,

    /// <summary>Body Y axis points down (right side down).</summary>
    YDown,

    /// <summary>Body Y axis points up (left side down).</summary>
    YUp,

    /// <summary>Body Z axis points down (level — the normal rest position).</summary>
    ZDown,

    /// <summary>Body Z axis points up (upside down).</summary>
    ZUp,
}
