using FluentAssertions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Calibration;

public class AccelerometerSixPositionCalibratorTests
{
    private static readonly Dictionary<AccelerometerCalibrationFace, Vector3> TrueReadings = new()
    {
        [AccelerometerCalibrationFace.XDown] = new Vector3(-1, 0, 0),
        [AccelerometerCalibrationFace.XUp] = new Vector3(1, 0, 0),
        [AccelerometerCalibrationFace.YDown] = new Vector3(0, -1, 0),
        [AccelerometerCalibrationFace.YUp] = new Vector3(0, 1, 0),
        [AccelerometerCalibrationFace.ZDown] = new Vector3(0, 0, -1),
        [AccelerometerCalibrationFace.ZUp] = new Vector3(0, 0, 1),
    };

    [Fact]
    public void Recovers_known_bias_and_scale_from_six_positions()
    {
        var trueBias = new Vector3(0.02, -0.03, 0.05);
        var trueScale = new Vector3(1.02, 0.98, 1.05);
        var calibrator = new AccelerometerSixPositionCalibrator();

        foreach ((AccelerometerCalibrationFace face, Vector3 trueValue) in TrueReadings)
        {
            // The sensor model inverts the correction: measured = true ⊘ scale + bias.
            var measured = new Vector3(
                (trueValue.X / trueScale.X) + trueBias.X,
                (trueValue.Y / trueScale.Y) + trueBias.Y,
                (trueValue.Z / trueScale.Z) + trueBias.Z);
            for (int i = 0; i < AccelerometerSixPositionCalibrator.MinimumSamplesPerFace; i++)
            {
                calibrator.AddSample(face, TestSamples.With(accel: measured));
            }
        }

        AccelerometerCalibrationResult result = calibrator.ComputeResult();

        result.Bias.X.Should().BeApproximately(trueBias.X, 1e-9);
        result.Bias.Y.Should().BeApproximately(trueBias.Y, 1e-9);
        result.Bias.Z.Should().BeApproximately(trueBias.Z, 1e-9);
        result.Scale.X.Should().BeApproximately(trueScale.X, 1e-9);
        result.Scale.Y.Should().BeApproximately(trueScale.Y, 1e-9);
        result.Scale.Z.Should().BeApproximately(trueScale.Z, 1e-9);

        // Round trip: applying the result levels the readings exactly.
        var profile = new CalibrationProfile
        {
            AccelerometerBias = result.Bias,
            AccelerometerScale = result.Scale,
        };
        SensorSample corrected = profile.Apply(TestSamples.With(accel: new Vector3(
            (TrueReadings[AccelerometerCalibrationFace.ZDown].X / trueScale.X) + trueBias.X,
            (TrueReadings[AccelerometerCalibrationFace.ZDown].Y / trueScale.Y) + trueBias.Y,
            (TrueReadings[AccelerometerCalibrationFace.ZDown].Z / trueScale.Z) + trueBias.Z)));
        corrected.Accelerometer.Z.Should().BeApproximately(-1, 1e-9);
    }

    [Fact]
    public void Missing_faces_are_reported_by_name()
    {
        var calibrator = new AccelerometerSixPositionCalibrator();
        for (int i = 0; i < AccelerometerSixPositionCalibrator.MinimumSamplesPerFace; i++)
        {
            calibrator.AddSample(AccelerometerCalibrationFace.ZDown, TestSamples.Level());
        }

        Action act = () => calibrator.ComputeResult();

        act.Should().Throw<CalibrationException>()
            .WithMessage("*XDown*")
            .WithMessage("*ZUp*");
    }

    [Fact]
    public void Implausibly_flat_readings_are_rejected()
    {
        var calibrator = new AccelerometerSixPositionCalibrator();
        foreach (AccelerometerCalibrationFace face in Enum.GetValues<AccelerometerCalibrationFace>())
        {
            for (int i = 0; i < AccelerometerSixPositionCalibrator.MinimumSamplesPerFace; i++)
            {
                // The device never actually moved: every face reads level.
                calibrator.AddSample(face, TestSamples.Level());
            }
        }

        Action act = () => calibrator.ComputeResult();

        act.Should().Throw<CalibrationException>().WithMessage("*rested on each face*");
    }

    [Fact]
    public void Face_progress_is_reported_for_the_ui()
    {
        var calibrator = new AccelerometerSixPositionCalibrator();
        calibrator.IsFaceCaptured(AccelerometerCalibrationFace.XUp).Should().BeFalse();

        for (int i = 0; i < AccelerometerSixPositionCalibrator.MinimumSamplesPerFace; i++)
        {
            calibrator.AddSample(AccelerometerCalibrationFace.XUp, TestSamples.Level());
        }

        calibrator.IsFaceCaptured(AccelerometerCalibrationFace.XUp).Should().BeTrue();
        calibrator.CapturedFaces.Should().ContainSingle()
            .Which.Should().Be(AccelerometerCalibrationFace.XUp);
    }
}
