using System.Globalization;

namespace ImuGui.Core.Models;

/// <summary>An immutable three-component vector of doubles.</summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
public readonly record struct Vector3(double X, double Y, double Z)
{
    private const double NormalizationEpsilon = 1e-12;

    /// <summary>The vector (0, 0, 0).</summary>
    public static Vector3 Zero { get; } = new(0, 0, 0);

    /// <summary>The vector (1, 0, 0).</summary>
    public static Vector3 UnitX { get; } = new(1, 0, 0);

    /// <summary>The vector (0, 1, 0).</summary>
    public static Vector3 UnitY { get; } = new(0, 1, 0);

    /// <summary>The vector (0, 0, 1).</summary>
    public static Vector3 UnitZ { get; } = new(0, 0, 1);

    /// <summary>The Euclidean length of the vector.</summary>
    public double Magnitude => Math.Sqrt(MagnitudeSquared);

    /// <summary>The squared Euclidean length of the vector.</summary>
    public double MagnitudeSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>Adds two vectors component-wise.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    public static Vector3 operator +(Vector3 left, Vector3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>Subtracts <paramref name="right"/> from <paramref name="left"/> component-wise.</summary>
    /// <param name="left">The vector to subtract from.</param>
    /// <param name="right">The vector to subtract.</param>
    public static Vector3 operator -(Vector3 left, Vector3 right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>Negates every component of the vector.</summary>
    /// <param name="value">The vector to negate.</param>
    public static Vector3 operator -(Vector3 value) => new(-value.X, -value.Y, -value.Z);

    /// <summary>Multiplies every component by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar factor.</param>
    public static Vector3 operator *(Vector3 vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>Multiplies every component by a scalar.</summary>
    /// <param name="scalar">The scalar factor.</param>
    /// <param name="vector">The vector.</param>
    public static Vector3 operator *(double scalar, Vector3 vector) => vector * scalar;

    /// <summary>Divides every component by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar divisor.</param>
    public static Vector3 operator /(Vector3 vector, double scalar) =>
        new(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);

    /// <summary>Computes the dot product with another vector.</summary>
    /// <param name="other">The other vector.</param>
    public double Dot(Vector3 other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    /// <summary>Computes the right-handed cross product with another vector.</summary>
    /// <param name="other">The other vector.</param>
    public Vector3 Cross(Vector3 other) => new(
        (Y * other.Z) - (Z * other.Y),
        (Z * other.X) - (X * other.Z),
        (X * other.Y) - (Y * other.X));

    /// <summary>Multiplies component-wise by per-axis factors (Hadamard product).</summary>
    /// <param name="factors">The per-component factors.</param>
    public Vector3 Scale(Vector3 factors) => new(X * factors.X, Y * factors.Y, Z * factors.Z);

    /// <summary>Returns a unit-length vector pointing in the same direction.</summary>
    /// <exception cref="InvalidOperationException">The vector is too close to zero length to normalize.</exception>
    public Vector3 Normalized()
    {
        double magnitude = Magnitude;
        if (magnitude < NormalizationEpsilon)
        {
            throw new InvalidOperationException("Cannot normalize a vector of (near-)zero length.");
        }

        return this / magnitude;
    }

    /// <summary>Formats the vector with invariant culture, e.g. <c>(0.1, -2.3, 4.5)</c>.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"({X:G6}, {Y:G6}, {Z:G6})");
}
