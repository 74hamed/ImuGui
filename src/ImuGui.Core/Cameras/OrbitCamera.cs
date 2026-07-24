using ImuGui.Core.Fusion;
using ImuGui.Core.Models;

namespace ImuGui.Core.Cameras;

/// <summary>
/// An orbit camera for the 3-D environment view: yaw/pitch around a movable target point
/// at a zoomable distance. Pure math (right-handed, Y-up graphics convention — the
/// renderer maps sensor frames into it), fully unit-testable, with an explicitly
/// initialized rotation center and sane defaults.
/// <para>
/// Documented control mapping (implemented by the view): left-drag orbits, Ctrl+drag pans,
/// mouse wheel zooms, and Reset restores this default pose.
/// </para>
/// </summary>
public sealed class OrbitCamera
{
    /// <summary>The closest allowed zoom distance.</summary>
    public const double MinimumDistance = 1.0;

    /// <summary>The farthest allowed zoom distance.</summary>
    public const double MaximumDistance = 60.0;

    private const double DefaultYawRadians = Math.PI / 4;      // 45° around the scene
    private const double DefaultPitchRadians = Math.PI / 6;    // 30° above the horizon
    private const double DefaultDistance = 8.0;
    private static readonly double PitchLimitRadians = AngleMath.DegreesToRadians(89.0);
    private static readonly Vector3 WorldUp = Vector3.UnitY;

    /// <summary>Creates the camera at its default pose.</summary>
    public OrbitCamera() => Reset();

    /// <summary>Azimuth around the target in radians.</summary>
    public double YawRadians { get; private set; }

    /// <summary>Elevation above the target's horizontal plane in radians, clamped to ±89°.</summary>
    public double PitchRadians { get; private set; }

    /// <summary>Distance from the target, clamped to [<see cref="MinimumDistance"/>, <see cref="MaximumDistance"/>].</summary>
    public double Distance { get; private set; }

    /// <summary>The orbit/rotation center. Always initialized; moved by <see cref="Pan"/>.</summary>
    public Vector3 Target { get; private set; }

    /// <summary>The current eye/target/up pose for a look-at matrix.</summary>
    public CameraPose Pose
    {
        get
        {
            Vector3 offsetDirection = OffsetDirection();
            Vector3 eye = Target + (offsetDirection * Distance);
            Vector3 forward = (-offsetDirection).Normalized();
            Vector3 right = forward.Cross(WorldUp).Normalized();
            Vector3 up = right.Cross(forward);
            return new CameraPose(eye, Target, up);
        }
    }

    /// <summary>Orbits around the target (left-drag).</summary>
    /// <param name="deltaYawRadians">Azimuth change in radians.</param>
    /// <param name="deltaPitchRadians">Elevation change in radians.</param>
    public void Orbit(double deltaYawRadians, double deltaPitchRadians)
    {
        YawRadians = AngleMath.WrapRadiansSigned(YawRadians + deltaYawRadians);
        PitchRadians = Math.Clamp(
            PitchRadians + deltaPitchRadians, -PitchLimitRadians, PitchLimitRadians);
    }

    /// <summary>
    /// Moves the target (and thus the camera) along the camera's right/up axes (Ctrl+drag).
    /// Distances are in world units at the target's depth.
    /// </summary>
    /// <param name="deltaRight">Movement along the camera's right axis.</param>
    /// <param name="deltaUp">Movement along the camera's up axis.</param>
    public void Pan(double deltaRight, double deltaUp)
    {
        CameraPose pose = Pose;
        Vector3 forward = (pose.Target - pose.EyePosition).Normalized();
        Vector3 right = forward.Cross(WorldUp).Normalized();
        Target += (right * deltaRight) + (pose.Up * deltaUp);
    }

    /// <summary>Zooms by a multiplicative factor (mouse wheel); &lt; 1 moves closer.</summary>
    /// <param name="factor">The distance multiplier; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="factor"/> is not positive.</exception>
    public void Zoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Zoom factor must be positive.");
        }

        Distance = Math.Clamp(Distance * factor, MinimumDistance, MaximumDistance);
    }

    /// <summary>Restores the default pose (45° azimuth, 30° elevation, distance 8, target at origin).</summary>
    public void Reset()
    {
        YawRadians = DefaultYawRadians;
        PitchRadians = DefaultPitchRadians;
        Distance = DefaultDistance;
        Target = Vector3.Zero;
    }

    private Vector3 OffsetDirection()
    {
        double cosPitch = Math.Cos(PitchRadians);
        return new Vector3(
            cosPitch * Math.Sin(YawRadians),
            Math.Sin(PitchRadians),
            cosPitch * Math.Cos(YawRadians));
    }
}
