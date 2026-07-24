using FluentAssertions;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using Xunit;

namespace ImuGui.Core.Tests.Models;

public class QuaternionTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Identity_leaves_vectors_unchanged()
    {
        var vector = new Vector3(1, -2, 3);
        Quaternion.Identity.Rotate(vector).Should().Be(vector);
        Quaternion.Identity.ToOrientation().Should().Be(Orientation.Zero);
    }

    [Fact]
    public void Ninety_degree_yaw_rotates_x_to_y()
    {
        // +90° about Z (right-hand rule): X → Y.
        Quaternion yaw = Quaternion.FromAxisAngle(Vector3.UnitZ, Math.PI / 2);

        Vector3 rotated = yaw.Rotate(Vector3.UnitX);
        rotated.X.Should().BeApproximately(0, Tolerance);
        rotated.Y.Should().BeApproximately(1, Tolerance);
        rotated.Z.Should().BeApproximately(0, Tolerance);
    }

    [Fact]
    public void InverseRotate_undoes_Rotate()
    {
        Quaternion attitude = Quaternion.FromEulerAngles(0.4, -0.7, 2.1);
        var vector = new Vector3(0.3, -1.2, 2.5);

        Vector3 roundTripped = attitude.InverseRotate(attitude.Rotate(vector));

        roundTripped.X.Should().BeApproximately(vector.X, Tolerance);
        roundTripped.Y.Should().BeApproximately(vector.Y, Tolerance);
        roundTripped.Z.Should().BeApproximately(vector.Z, Tolerance);
    }

    [Theory]
    [InlineData(30, 0, 0)]
    [InlineData(0, 45, 0)]
    [InlineData(0, 0, 120)]
    [InlineData(-25, 40, 310)]
    [InlineData(170, -60, 15)]
    public void Euler_round_trip_recovers_angles(double rollDeg, double pitchDeg, double yawDeg)
    {
        Quaternion attitude = Quaternion.FromEulerAngles(
            AngleMath.DegreesToRadians(rollDeg),
            AngleMath.DegreesToRadians(pitchDeg),
            AngleMath.DegreesToRadians(yawDeg));

        Orientation orientation = attitude.ToOrientation();

        orientation.RollDegrees.Should().BeApproximately(rollDeg, 1e-9);
        orientation.PitchDegrees.Should().BeApproximately(pitchDeg, 1e-9);
        orientation.YawDegrees.Should().BeApproximately(AngleMath.WrapDegreesPositive(yawDeg), 1e-9);
    }

    [Fact]
    public void Composition_applies_right_operand_first()
    {
        Quaternion pitch = Quaternion.FromAxisAngle(Vector3.UnitY, Math.PI / 2);
        Quaternion yaw = Quaternion.FromAxisAngle(Vector3.UnitZ, Math.PI / 2);

        // (yaw * pitch) applies pitch first: X → (pitch: X→−Z?) — verify numerically against
        // sequential application.
        Vector3 sequential = yaw.Rotate(pitch.Rotate(Vector3.UnitX));
        Vector3 composed = (yaw * pitch).Rotate(Vector3.UnitX);

        composed.X.Should().BeApproximately(sequential.X, Tolerance);
        composed.Y.Should().BeApproximately(sequential.Y, Tolerance);
        composed.Z.Should().BeApproximately(sequential.Z, Tolerance);
    }

    [Fact]
    public void Normalized_returns_unit_quaternion_and_rejects_zero()
    {
        var scaled = new Quaternion(2, 0, 0, 0);
        scaled.Normalized().Should().Be(Quaternion.Identity);

        Action act = () => new Quaternion(0, 0, 0, 0).Normalized();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Gimbal_edge_at_ninety_degrees_pitch_is_clamped_not_NaN()
    {
        Quaternion attitude = Quaternion.FromEulerAngles(0, Math.PI / 2, 0);
        Orientation orientation = attitude.ToOrientation();

        orientation.PitchDegrees.Should().BeApproximately(90, 1e-6);
        double.IsFinite(orientation.RollDegrees).Should().BeTrue();
        double.IsFinite(orientation.YawDegrees).Should().BeTrue();
    }
}
