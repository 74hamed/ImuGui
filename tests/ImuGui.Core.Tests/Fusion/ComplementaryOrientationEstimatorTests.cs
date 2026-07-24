using FluentAssertions;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Fusion;

public class ComplementaryOrientationEstimatorTests
{
    private static readonly TimeSpan Dt20Ms = TimeSpan.FromMilliseconds(20);

    [Fact]
    public void First_update_initializes_from_accel_and_mag()
    {
        var estimator = new ComplementaryOrientationEstimator();
        SensorSample sample = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(15), AngleMath.DegreesToRadians(10), AngleMath.DegreesToRadians(200));

        estimator.Update(sample, TimeSpan.Zero);

        Orientation orientation = estimator.CurrentOrientation;
        orientation.RollDegrees.Should().BeApproximately(15, 1e-6);
        orientation.PitchDegrees.Should().BeApproximately(10, 1e-6);
        orientation.YawDegrees.Should().BeApproximately(200, 1e-6);
    }

    [Fact]
    public void Holds_a_static_attitude()
    {
        var estimator = new ComplementaryOrientationEstimator();
        SensorSample sample = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(15), AngleMath.DegreesToRadians(10), AngleMath.DegreesToRadians(200));

        for (int i = 0; i < 500; i++)
        {
            estimator.Update(sample, Dt20Ms);
        }

        Orientation orientation = estimator.CurrentOrientation;
        orientation.RollDegrees.Should().BeApproximately(15, 0.5);
        orientation.PitchDegrees.Should().BeApproximately(10, 0.5);
        orientation.YawDegrees.Should().BeApproximately(200, 0.5);
    }

    [Fact]
    public void Gyro_only_propagation_uses_measured_dt()
    {
        var estimator = new ComplementaryOrientationEstimator();
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        // 30 °/s about X for 1.5 s of measured time in irregular chunks → 45°.
        SensorSample rolling = TestSamples.GyroOnly(30, 0, 0);
        for (int i = 0; i < 30; i++)
        {
            estimator.Update(rolling, TimeSpan.FromMilliseconds(10));
            estimator.Update(rolling, TimeSpan.FromMilliseconds(40));
        }

        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(45, 0.5);
    }

    [Fact]
    public void Converges_to_a_measured_yaw_change_within_a_few_time_constants()
    {
        var estimator = new ComplementaryOrientationEstimator(
            new ComplementaryOptions { TimeConstantSeconds = 1.0 });
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        SensorSample facingEast = TestSamples.FromAttitude(0, 0, Math.PI / 2);
        for (int i = 0; i < 500; i++) // 10 s = 10 time constants
        {
            estimator.Update(facingEast, Dt20Ms);
        }

        estimator.CurrentOrientation.YawDegrees.Should().BeApproximately(90, 1.0);
    }

    [Fact]
    public void Yaw_blending_is_wrap_aware_near_north()
    {
        var estimator = new ComplementaryOrientationEstimator();
        SensorSample nearNorth = TestSamples.FromAttitude(0, 0, AngleMath.DegreesToRadians(355));
        estimator.Update(nearNorth, TimeSpan.Zero);

        for (int i = 0; i < 500; i++)
        {
            estimator.Update(nearNorth, Dt20Ms);
        }

        // A naive average of 355° and a −5°-equivalent prediction would drift toward 180°.
        estimator.CurrentOrientation.YawDegrees.Should().BeApproximately(355, 0.5);
    }

    [Fact]
    public void Reset_reinitializes_from_the_next_sample()
    {
        var estimator = new ComplementaryOrientationEstimator();
        estimator.Update(TestSamples.FromAttitude(0.7, -0.2, 2.0), TimeSpan.Zero);

        estimator.Reset();
        estimator.CurrentOrientation.Should().Be(Orientation.Zero);

        estimator.Update(TestSamples.FromAttitude(0, AngleMath.DegreesToRadians(-30), 0), TimeSpan.Zero);
        estimator.CurrentOrientation.PitchDegrees.Should().BeApproximately(-30, 1e-6);
    }

    [Fact]
    public void Estimator_factory_creates_the_requested_kinds()
    {
        OrientationEstimatorFactory.Create(OrientationEstimatorKind.MahonyQuaternion)
            .Should().BeOfType<MahonyOrientationEstimator>();
        OrientationEstimatorFactory.Create(OrientationEstimatorKind.EulerComplementary)
            .Should().BeOfType<ComplementaryOrientationEstimator>();
    }
}
