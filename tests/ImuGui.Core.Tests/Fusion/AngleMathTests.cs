using FluentAssertions;
using ImuGui.Core.Fusion;
using Xunit;

namespace ImuGui.Core.Tests.Fusion;

public class AngleMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(360, 0)]
    [InlineData(-10, 350)]
    [InlineData(725, 5)]
    [InlineData(-725, 355)]
    public void WrapDegreesPositive_maps_into_zero_to_360(double input, double expected) =>
        AngleMath.WrapDegreesPositive(input).Should().BeApproximately(expected, 1e-12);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 180)]
    [InlineData(-180, 180)]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    public void WrapDegreesSigned_maps_into_signed_range(double input, double expected) =>
        AngleMath.WrapDegreesSigned(input).Should().BeApproximately(expected, 1e-12);

    [Theory]
    [InlineData(350, 10, 20)]
    [InlineData(10, 350, -20)]
    [InlineData(0, 180, 180)]
    [InlineData(90, 90, 0)]
    public void ShortestDifferenceDegrees_is_wrap_aware(double from, double to, double expected) =>
        AngleMath.ShortestDifferenceDegrees(from, to).Should().BeApproximately(expected, 1e-12);

    [Fact]
    public void Degree_radian_conversions_round_trip()
    {
        AngleMath.RadiansToDegrees(AngleMath.DegreesToRadians(123.456))
            .Should().BeApproximately(123.456, 1e-12);
        AngleMath.DegreesToRadians(180).Should().BeApproximately(Math.PI, 1e-15);
    }

    [Fact]
    public void WrapRadiansSigned_handles_edges()
    {
        AngleMath.WrapRadiansSigned(Math.PI).Should().BeApproximately(Math.PI, 1e-12);
        AngleMath.WrapRadiansSigned(-Math.PI).Should().BeApproximately(Math.PI, 1e-12);
        AngleMath.WrapRadiansSigned(3 * Math.PI).Should().BeApproximately(Math.PI, 1e-12);
        AngleMath.WrapRadiansSigned(0.5).Should().BeApproximately(0.5, 1e-12);
    }
}
