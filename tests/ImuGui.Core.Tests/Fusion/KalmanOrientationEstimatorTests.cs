using FluentAssertions;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Fusion;

public class KalmanOrientationEstimatorTests
{
    private static readonly TimeSpan Dt20Ms = TimeSpan.FromMilliseconds(20);

    [Fact]
    public void First_update_initializes_from_accel_and_mag()
    {
        var estimator = new KalmanOrientationEstimator();
        SensorSample sample = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(10), AngleMath.DegreesToRadians(-20), AngleMath.DegreesToRadians(250));

        estimator.Update(sample, TimeSpan.Zero);

        Orientation orientation = estimator.CurrentOrientation;
        orientation.RollDegrees.Should().BeApproximately(10, 1e-6);
        orientation.PitchDegrees.Should().BeApproximately(-20, 1e-6);
        orientation.YawDegrees.Should().BeApproximately(250, 1e-6);
    }

    [Fact]
    public void Holds_a_static_attitude()
    {
        var estimator = new KalmanOrientationEstimator();
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
    public void Gyro_only_prediction_uses_measured_dt()
    {
        var estimator = new KalmanOrientationEstimator();
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        // 45 °/s about X for exactly 2 s of measured time delivered as irregular
        // 10/30 ms intervals; with accel/mag absent the filter only predicts.
        SensorSample rolling = TestSamples.GyroOnly(45, 0, 0);
        for (int i = 0; i < 50; i++)
        {
            estimator.Update(rolling, TimeSpan.FromMilliseconds(10));
            estimator.Update(rolling, TimeSpan.FromMilliseconds(30));
        }

        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(90, 1.0);
    }

    [Fact]
    public void Constant_gyro_bias_is_learned_and_removed_by_the_bias_state()
    {
        var estimator = new KalmanOrientationEstimator();
        SensorSample levelWithBias = TestSamples.Level() with { Gyroscope = new Vector3(3, 0, 0) };
        estimator.Update(levelWithBias, TimeSpan.Zero);

        // 60 simulated seconds of a stationary, level device whose gyro lies by 3 °/s:
        // the [angle, bias] state must converge with the bias absorbed, not a tilted angle.
        for (int i = 0; i < 3000; i++)
        {
            estimator.Update(levelWithBias, Dt20Ms);
        }

        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(0, 1.0,
            "the Kalman bias state absorbs a constant rate offset");
    }

    [Fact]
    public void Yaw_correction_is_wrap_aware_near_north()
    {
        var estimator = new KalmanOrientationEstimator();
        SensorSample nearNorth = TestSamples.FromAttitude(0, 0, AngleMath.DegreesToRadians(355));
        estimator.Update(nearNorth, TimeSpan.Zero);

        for (int i = 0; i < 500; i++)
        {
            estimator.Update(nearNorth, Dt20Ms);
        }

        estimator.CurrentOrientation.YawDegrees.Should().BeApproximately(355, 0.5);
    }

    [Fact]
    public void Reset_reinitializes_from_the_next_sample()
    {
        var estimator = new KalmanOrientationEstimator();
        estimator.Update(TestSamples.FromAttitude(0.5, 0.2, 1.5), TimeSpan.Zero);

        estimator.Reset();
        estimator.CurrentOrientation.Should().Be(Orientation.Zero);

        estimator.Update(TestSamples.FromAttitude(AngleMath.DegreesToRadians(25), 0, 0), TimeSpan.Zero);
        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(25, 1e-6);
    }

    [Fact]
    public void Factory_creates_the_kalman_estimator()
    {
        OrientationEstimatorFactory.Create(OrientationEstimatorKind.KalmanEuler)
            .Should().BeOfType<KalmanOrientationEstimator>();
    }

    [Fact]
    public void Invalid_options_are_rejected()
    {
        Action zeroR = () => _ = new KalmanOrientationEstimator(
            new KalmanEstimatorOptions { MeasurementNoise = 0 });
        zeroR.Should().Throw<ArgumentOutOfRangeException>();

        Action negativeQ = () => _ = new KalmanOrientationEstimator(
            new KalmanEstimatorOptions { AngleProcessNoise = -1 });
        negativeQ.Should().Throw<ArgumentOutOfRangeException>();
    }
}
