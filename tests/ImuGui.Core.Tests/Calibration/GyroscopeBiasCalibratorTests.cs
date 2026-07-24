using FluentAssertions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Calibration;

public class GyroscopeBiasCalibratorTests
{
    [Fact]
    public void Recovers_a_known_bias_from_noisy_stationary_samples()
    {
        var trueBias = new Vector3(0.35, -0.22, 0.13);
        var calibrator = new GyroscopeBiasCalibrator();
        var random = new Random(7);

        for (int i = 0; i < 500; i++)
        {
            var noise = new Vector3(
                (random.NextDouble() - 0.5) * 0.2,
                (random.NextDouble() - 0.5) * 0.2,
                (random.NextDouble() - 0.5) * 0.2);
            calibrator.AddSample(TestSamples.With(gyro: trueBias + noise));
        }

        Vector3 recovered = calibrator.ComputeBias();

        recovered.X.Should().BeApproximately(trueBias.X, 0.02);
        recovered.Y.Should().BeApproximately(trueBias.Y, 0.02);
        recovered.Z.Should().BeApproximately(trueBias.Z, 0.02);
    }

    [Fact]
    public void Refuses_to_compute_from_too_few_samples()
    {
        var calibrator = new GyroscopeBiasCalibrator();
        calibrator.AddSample(TestSamples.Level());

        Action act = () => calibrator.ComputeBias();

        act.Should().Throw<CalibrationException>().WithMessage("*still*");
        calibrator.HasEnoughSamples.Should().BeFalse();
    }

    [Fact]
    public void Reset_discards_captured_samples()
    {
        var calibrator = new GyroscopeBiasCalibrator();
        for (int i = 0; i < 30; i++)
        {
            calibrator.AddSample(TestSamples.Level());
        }

        calibrator.Reset();

        calibrator.SampleCount.Should().Be(0);
    }
}
