using FluentAssertions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Calibration;

public class MagnetometerCalibratorTests
{
    private const double FieldStrength = 50.0;

    private static IEnumerable<Vector3> SphereSweep()
    {
        // 30° longitude steps land exactly on all six ±axis extremes, which the
        // min/max method needs to see to recover the true per-axis radii.
        for (int latitudeDeg = -90; latitudeDeg <= 90; latitudeDeg += 15)
        {
            for (int longitudeDeg = 0; longitudeDeg < 360; longitudeDeg += 30)
            {
                double lat = latitudeDeg * Math.PI / 180;
                double lon = longitudeDeg * Math.PI / 180;
                yield return new Vector3(
                    FieldStrength * Math.Cos(lat) * Math.Cos(lon),
                    FieldStrength * Math.Cos(lat) * Math.Sin(lon),
                    FieldStrength * Math.Sin(lat));
            }
        }
    }

    [Fact]
    public void Recovers_hard_iron_offset_and_equalizes_soft_iron_axes()
    {
        var hardIron = new Vector3(3, -2, 4);
        var softIronDistortion = new Vector3(1.0, 1.0, 1.25); // Z axis compressed by the sensor
        var calibrator = new MagnetometerCalibrator();

        var measuredPoints = new List<Vector3>();
        foreach (Vector3 truePoint in SphereSweep())
        {
            var measured = new Vector3(
                (truePoint.X / softIronDistortion.X) + hardIron.X,
                (truePoint.Y / softIronDistortion.Y) + hardIron.Y,
                (truePoint.Z / softIronDistortion.Z) + hardIron.Z);
            measuredPoints.Add(measured);
            calibrator.AddSample(TestSamples.With(mag: measured));
        }

        MagnetometerCalibrationResult result = calibrator.ComputeResult();

        result.HardIronOffset.X.Should().BeApproximately(hardIron.X, 1e-9);
        result.HardIronOffset.Y.Should().BeApproximately(hardIron.Y, 1e-9);
        result.HardIronOffset.Z.Should().BeApproximately(hardIron.Z, 1e-9);

        // After correction all points must lie on one sphere (constant magnitude).
        var profile = new CalibrationProfile
        {
            MagnetometerHardIronOffset = result.HardIronOffset,
            MagnetometerSoftIronScale = result.SoftIronScale,
        };
        double[] magnitudes = measuredPoints
            .Select(p => profile.Apply(TestSamples.With(mag: p)).Magnetometer.Magnitude)
            .ToArray();
        double mean = magnitudes.Average();
        foreach (double magnitude in magnitudes)
        {
            magnitude.Should().BeApproximately(mean, mean * 1e-6);
        }
    }

    [Fact]
    public void Flat_coverage_is_rejected_with_a_figure_eight_hint()
    {
        var calibrator = new MagnetometerCalibrator();
        for (int i = 0; i < 200; i++)
        {
            double angle = i * Math.PI / 100;
            // Device only spun about one axis: Z barely changes.
            calibrator.AddSample(TestSamples.With(mag: new Vector3(
                FieldStrength * Math.Cos(angle), FieldStrength * Math.Sin(angle), 0.01 * Math.Sin(angle))));
        }

        Action act = () => calibrator.ComputeResult();

        act.Should().Throw<CalibrationException>().WithMessage("*figure-eight*");
    }

    [Fact]
    public void Too_few_samples_are_rejected_with_the_count_in_the_message()
    {
        var calibrator = new MagnetometerCalibrator();
        for (int i = 0; i < 10; i++)
        {
            calibrator.AddSample(TestSamples.Level());
        }

        Action act = () => calibrator.ComputeResult();

        act.Should().Throw<CalibrationException>().WithMessage($"*{MagnetometerCalibrator.MinimumSampleCount}*");
    }

    [Fact]
    public void Live_extremes_are_exposed_for_coverage_display()
    {
        var calibrator = new MagnetometerCalibrator();
        calibrator.AddSample(TestSamples.With(mag: new Vector3(10, -5, 3)));
        calibrator.AddSample(TestSamples.With(mag: new Vector3(-20, 8, 1)));

        calibrator.CurrentMinimum.Should().Be(new Vector3(-20, -5, 1));
        calibrator.CurrentMaximum.Should().Be(new Vector3(10, 8, 3));
    }
}
