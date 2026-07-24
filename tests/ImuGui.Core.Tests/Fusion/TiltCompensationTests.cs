using FluentAssertions;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Fusion;

public class TiltCompensationTests
{
    [Fact]
    public void Level_accelerometer_reading_gives_zero_roll_and_pitch()
    {
        var level = new Vector3(0, 0, -1);
        TiltCompensation.ComputeRollRadians(level).Should().BeApproximately(0, 1e-12);
        TiltCompensation.ComputePitchRadians(level).Should().BeApproximately(0, 1e-12);
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(-45, 0)]
    [InlineData(0, 30)]
    [InlineData(0, -60)]
    [InlineData(25, -35)]
    public void Roll_and_pitch_are_recovered_from_static_attitudes(double rollDeg, double pitchDeg)
    {
        SensorSample sample = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(rollDeg), AngleMath.DegreesToRadians(pitchDeg), 0);

        AngleMath.RadiansToDegrees(TiltCompensation.ComputeRollRadians(sample.Accelerometer))
            .Should().BeApproximately(rollDeg, 1e-9);
        AngleMath.RadiansToDegrees(TiltCompensation.ComputePitchRadians(sample.Accelerometer))
            .Should().BeApproximately(pitchDeg, 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(270)]
    [InlineData(350)]
    public void Heading_matches_yaw_for_a_level_device(double yawDeg)
    {
        SensorSample sample = TestSamples.FromAttitude(0, 0, AngleMath.DegreesToRadians(yawDeg));

        TiltCompensation.ComputeHeadingDegrees(sample.Magnetometer, 0, 0)
            .Should().BeApproximately(yawDeg, 1e-9);
    }

    [Theory]
    [InlineData(20, -15, 135)]
    [InlineData(-30, 25, 300)]
    [InlineData(45, 10, 80)]
    public void Heading_is_tilt_compensated_for_rolled_and_pitched_devices(
        double rollDeg, double pitchDeg, double yawDeg)
    {
        double roll = AngleMath.DegreesToRadians(rollDeg);
        double pitch = AngleMath.DegreesToRadians(pitchDeg);
        SensorSample sample = TestSamples.FromAttitude(roll, pitch, AngleMath.DegreesToRadians(yawDeg));

        // Without compensation the tilted field components would corrupt the heading;
        // de-rotating by the true roll/pitch must recover the true yaw.
        TiltCompensation.ComputeHeadingDegrees(sample.Magnetometer, roll, pitch)
            .Should().BeApproximately(yawDeg, 1e-9);
    }

    [Fact]
    public void East_heading_is_90_degrees_confirming_clockwise_positive_convention()
    {
        SensorSample facingEast = TestSamples.FromAttitude(0, 0, Math.PI / 2);
        TiltCompensation.ComputeHeadingDegrees(facingEast.Magnetometer, 0, 0)
            .Should().BeApproximately(90, 1e-9);
    }
}
