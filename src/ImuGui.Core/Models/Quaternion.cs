using System.Globalization;
using ImuGui.Core.Fusion;

namespace ImuGui.Core.Models;

/// <summary>
/// An immutable unit quaternion (Hamilton convention, scalar first) used to represent
/// a body-to-world attitude. ImuGui uses the NED convention throughout: world X north,
/// Y east, Z down; body X forward, Y right, Z down; Euler order ZYX (yaw-pitch-roll).
/// </summary>
/// <param name="W">The scalar component.</param>
/// <param name="X">The first vector component.</param>
/// <param name="Y">The second vector component.</param>
/// <param name="Z">The third vector component.</param>
public readonly record struct Quaternion(double W, double X, double Y, double Z)
{
    private const double NormalizationEpsilon = 1e-12;

    /// <summary>The identity rotation (no rotation).</summary>
    public static Quaternion Identity { get; } = new(1, 0, 0, 0);

    /// <summary>The Euclidean norm of the quaternion.</summary>
    public double Magnitude => Math.Sqrt(MagnitudeSquared);

    /// <summary>The squared Euclidean norm of the quaternion.</summary>
    public double MagnitudeSquared => (W * W) + (X * X) + (Y * Y) + (Z * Z);

    /// <summary>
    /// Composes two rotations with the Hamilton product; <c>(a * b)</c> applies
    /// <paramref name="b"/> first, then <paramref name="a"/>.
    /// </summary>
    /// <param name="a">The outer (second) rotation.</param>
    /// <param name="b">The inner (first) rotation.</param>
    public static Quaternion operator *(Quaternion a, Quaternion b) => new(
        (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z),
        (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
        (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
        (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W));

    /// <summary>Creates a rotation of <paramref name="angleRadians"/> around <paramref name="axis"/>.</summary>
    /// <param name="axis">The rotation axis; does not need to be normalized.</param>
    /// <param name="angleRadians">The rotation angle in radians (right-hand rule).</param>
    public static Quaternion FromAxisAngle(Vector3 axis, double angleRadians)
    {
        Vector3 unitAxis = axis.Normalized();
        double halfAngle = angleRadians / 2;
        double sinHalf = Math.Sin(halfAngle);
        return new Quaternion(
            Math.Cos(halfAngle), unitAxis.X * sinHalf, unitAxis.Y * sinHalf, unitAxis.Z * sinHalf);
    }

    /// <summary>
    /// Creates a body-to-world attitude from ZYX Euler angles: yaw about Z, then pitch
    /// about Y, then roll about X.
    /// </summary>
    /// <param name="rollRadians">Roll about body X, positive right-side down.</param>
    /// <param name="pitchRadians">Pitch about body Y, positive nose up.</param>
    /// <param name="yawRadians">Yaw about body Z, positive clockwise seen from above.</param>
    public static Quaternion FromEulerAngles(double rollRadians, double pitchRadians, double yawRadians)
    {
        Quaternion yaw = FromAxisAngle(Vector3.UnitZ, yawRadians);
        Quaternion pitch = FromAxisAngle(Vector3.UnitY, pitchRadians);
        Quaternion roll = FromAxisAngle(Vector3.UnitX, rollRadians);
        return yaw * pitch * roll;
    }

    /// <summary>Returns the conjugate (inverse rotation for unit quaternions).</summary>
    public Quaternion Conjugate() => new(W, -X, -Y, -Z);

    /// <summary>Returns a unit-norm copy of this quaternion.</summary>
    /// <exception cref="InvalidOperationException">The quaternion has (near-)zero norm.</exception>
    public Quaternion Normalized()
    {
        double magnitude = Magnitude;
        if (magnitude < NormalizationEpsilon)
        {
            throw new InvalidOperationException("Cannot normalize a quaternion of (near-)zero norm.");
        }

        return new Quaternion(W / magnitude, X / magnitude, Y / magnitude, Z / magnitude);
    }

    /// <summary>Rotates a body-frame vector into the world frame (applies R(q)).</summary>
    /// <param name="vector">The body-frame vector.</param>
    public Vector3 Rotate(Vector3 vector)
    {
        var imaginary = new Vector3(X, Y, Z);
        Vector3 t = 2 * imaginary.Cross(vector);
        return vector + (W * t) + imaginary.Cross(t);
    }

    /// <summary>Rotates a world-frame vector into the body frame (applies R(q) transposed).</summary>
    /// <param name="vector">The world-frame vector.</param>
    public Vector3 InverseRotate(Vector3 vector) => Conjugate().Rotate(vector);

    /// <summary>
    /// Converts the attitude to display-ready Euler angles: roll in (-180, 180],
    /// pitch in [-90, 90], yaw in [0, 360).
    /// </summary>
    public Orientation ToOrientation()
    {
        double rollRadians = Math.Atan2(2 * ((W * X) + (Y * Z)), 1 - (2 * ((X * X) + (Y * Y))));
        double sinPitch = Math.Clamp(2 * ((W * Y) - (X * Z)), -1.0, 1.0);
        double pitchRadians = Math.Asin(sinPitch);
        double yawRadians = Math.Atan2(2 * ((W * Z) + (X * Y)), 1 - (2 * ((Y * Y) + (Z * Z))));

        return new Orientation(
            AngleMath.RadiansToDegrees(rollRadians),
            AngleMath.RadiansToDegrees(pitchRadians),
            AngleMath.WrapDegreesPositive(AngleMath.RadiansToDegrees(yawRadians)));
    }

    /// <summary>Formats the quaternion with invariant culture as <c>(w, x, y, z)</c>.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"({W:G6}, {X:G6}, {Y:G6}, {Z:G6})");
}
