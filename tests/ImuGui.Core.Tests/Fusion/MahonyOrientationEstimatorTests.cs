using FluentAssertions;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Fusion;

public class MahonyOrientationEstimatorTests
{
    private static readonly TimeSpan Dt20Ms = TimeSpan.FromMilliseconds(20);

    [Fact]
    public void First_update_initializes_attitude_directly_from_accel_and_mag()
    {
        var estimator = new MahonyOrientationEstimator();
        SensorSample sample = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(10), AngleMath.DegreesToRadians(-20), AngleMath.DegreesToRadians(250));

        estimator.Update(sample, TimeSpan.Zero);

        Orientation orientation = estimator.CurrentOrientation;
        orientation.RollDegrees.Should().BeApproximately(10, 1e-6);
        orientation.PitchDegrees.Should().BeApproximately(-20, 1e-6);
        orientation.YawDegrees.Should().BeApproximately(250, 1e-6);
    }

    [Theory]
    [InlineData(30, 0, 0)]
    [InlineData(0, 25, 0)]
    [InlineData(-20, 15, 0)]
    public void Converges_to_a_static_tilt_from_a_level_start(double rollDeg, double pitchDeg, double yawDeg)
    {
        var estimator = new MahonyOrientationEstimator();
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        SensorSample target = TestSamples.FromAttitude(
            AngleMath.DegreesToRadians(rollDeg),
            AngleMath.DegreesToRadians(pitchDeg),
            AngleMath.DegreesToRadians(yawDeg));
        for (int i = 0; i < 3000; i++)
        {
            estimator.Update(target, Dt20Ms);
        }

        Orientation orientation = estimator.CurrentOrientation;
        orientation.RollDegrees.Should().BeApproximately(rollDeg, 1.0);
        orientation.PitchDegrees.Should().BeApproximately(pitchDeg, 1.0);
    }

    [Fact]
    public void Converges_to_a_pure_yaw_target_via_the_magnetometer()
    {
        var estimator = new MahonyOrientationEstimator();
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        // 120 simulated seconds: a 90° correction transits quickly, then the integral
        // term's windup (bounded by the anti-windup clamp) bleeds off.
        SensorSample facingEast = TestSamples.FromAttitude(0, 0, Math.PI / 2);
        for (int i = 0; i < 6000; i++)
        {
            estimator.Update(facingEast, Dt20Ms);
        }

        estimator.CurrentOrientation.YawDegrees.Should().BeApproximately(90, 2.0);
    }

    [Fact]
    public void Gyro_integration_uses_the_measured_dt_not_a_constant()
    {
        var estimator = new MahonyOrientationEstimator(new MahonyOptions { IntegralGain = 0 });
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);

        // 45 °/s about X for exactly 2 s of *measured* time, delivered as irregular
        // 10/30 ms intervals. A hardcoded dt (the legacy 0.016) would integrate
        // 45 · 0.016 · 100 = 72°, not 90°.
        SensorSample rolling = TestSamples.GyroOnly(45, 0, 0);
        for (int i = 0; i < 50; i++)
        {
            estimator.Update(rolling, TimeSpan.FromMilliseconds(10));
            estimator.Update(rolling, TimeSpan.FromMilliseconds(30));
        }

        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(90, 0.5);
    }

    [Fact]
    public void Integral_term_absorbs_a_constant_gyro_bias()
    {
        var estimator = new MahonyOrientationEstimator();
        SensorSample levelWithBias = TestSamples.Level() with { Gyroscope = new Vector3(2, 0, 0) };
        estimator.Update(levelWithBias, TimeSpan.Zero);

        for (int i = 0; i < 5000; i++)
        {
            estimator.Update(levelWithBias, Dt20Ms);
        }

        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(0, 2.0,
            "the PI correction holds attitude against a 2 °/s bias");
    }

    [Fact]
    public void Ninety_degree_pitch_produces_finite_output_no_gimbal_lock()
    {
        var estimator = new MahonyOrientationEstimator();
        SensorSample noseUp = TestSamples.FromAttitude(0, Math.PI / 2, 0);
        estimator.Update(noseUp, TimeSpan.Zero);
        for (int i = 0; i < 500; i++)
        {
            estimator.Update(noseUp, Dt20Ms);
        }

        Orientation orientation = estimator.CurrentOrientation;
        double.IsFinite(orientation.RollDegrees).Should().BeTrue();
        double.IsFinite(orientation.YawDegrees).Should().BeTrue();
        orientation.PitchDegrees.Should().BeApproximately(90, 2.0);
    }

    [Fact]
    public void Reset_discards_state_and_reinitializes_from_the_next_sample()
    {
        var estimator = new MahonyOrientationEstimator();
        estimator.Update(TestSamples.FromAttitude(0.5, 0.3, 1.0), TimeSpan.Zero);

        estimator.Reset();
        estimator.CurrentAttitude.Should().Be(Quaternion.Identity);

        estimator.Update(TestSamples.FromAttitude(AngleMath.DegreesToRadians(12), 0, 0), TimeSpan.Zero);
        estimator.CurrentOrientation.RollDegrees.Should().BeApproximately(12, 1e-6);
    }

    [Fact]
    public void Vanishingly_small_rates_do_not_crash_the_integration()
    {
        // Regression (caught by CI on a different CPU): a corrected rate that is nonzero
        // but below the vector-normalization epsilon must be skipped, not integrated —
        // FromAxisAngle cannot normalize a (near-)zero axis. 1e-10 deg/s ≈ 1.7e-12 rad/s
        // lands deterministically inside that gap.
        var estimator = new MahonyOrientationEstimator(new MahonyOptions { IntegralGain = 0 });
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);
        Quaternion before = estimator.CurrentAttitude;

        estimator.Update(TestSamples.GyroOnly(1e-10, 0, 0), Dt20Ms);

        estimator.CurrentAttitude.Should().Be(before, "a sub-epsilon rotation is a no-op");
    }

    [Fact]
    public void Zero_and_negative_dt_do_not_advance_the_attitude()
    {
        var estimator = new MahonyOrientationEstimator();
        estimator.Update(TestSamples.Level(), TimeSpan.Zero);
        Quaternion before = estimator.CurrentAttitude;

        estimator.Update(TestSamples.GyroOnly(500, 500, 500), TimeSpan.Zero);
        estimator.Update(TestSamples.GyroOnly(500, 500, 500), TimeSpan.FromMilliseconds(-50));

        estimator.CurrentAttitude.Should().Be(before);
    }
}
