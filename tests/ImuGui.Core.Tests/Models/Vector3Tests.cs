using FluentAssertions;
using ImuGui.Core.Models;
using Xunit;

namespace ImuGui.Core.Tests.Models;

public class Vector3Tests
{
    [Fact]
    public void Arithmetic_operators_work_componentwise()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, -5, 6);

        (a + b).Should().Be(new Vector3(5, -3, 9));
        (a - b).Should().Be(new Vector3(-3, 7, -3));
        (-a).Should().Be(new Vector3(-1, -2, -3));
        (a * 2).Should().Be(new Vector3(2, 4, 6));
        (2 * a).Should().Be(new Vector3(2, 4, 6));
        (a / 2).Should().Be(new Vector3(0.5, 1, 1.5));
        a.Scale(b).Should().Be(new Vector3(4, -10, 18));
    }

    [Fact]
    public void Dot_and_cross_products_are_correct()
    {
        new Vector3(1, 2, 3).Dot(new Vector3(4, 5, 6)).Should().Be(32);

        // Right-handed basis: X × Y = Z.
        Vector3.UnitX.Cross(Vector3.UnitY).Should().Be(Vector3.UnitZ);
        Vector3.UnitY.Cross(Vector3.UnitZ).Should().Be(Vector3.UnitX);
        Vector3.UnitZ.Cross(Vector3.UnitX).Should().Be(Vector3.UnitY);
    }

    [Fact]
    public void Magnitude_and_normalization_are_correct()
    {
        var vector = new Vector3(3, 4, 0);
        vector.Magnitude.Should().Be(5);
        vector.MagnitudeSquared.Should().Be(25);

        Vector3 unit = vector.Normalized();
        unit.Should().Be(new Vector3(0.6, 0.8, 0));
        unit.Magnitude.Should().BeApproximately(1, 1e-12);
    }

    [Fact]
    public void Normalizing_a_zero_vector_throws()
    {
        Action act = () => Vector3.Zero.Normalized();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToString_uses_invariant_culture()
    {
        new Vector3(0.5, -1.25, 3).ToString().Should().Be("(0.5, -1.25, 3)");
    }
}
