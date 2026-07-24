using FluentAssertions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Models;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Calibration;

public class CalibrationProfileAndServiceTests
{
    [Fact]
    public void Profile_applies_bias_scale_and_iron_corrections()
    {
        var profile = new CalibrationProfile
        {
            GyroscopeBias = new Vector3(1, 2, 3),
            AccelerometerBias = new Vector3(0.1, 0, 0),
            AccelerometerScale = new Vector3(2, 1, 1),
            MagnetometerHardIronOffset = new Vector3(5, 5, 5),
            MagnetometerSoftIronScale = new Vector3(1, 1, 0.5),
        };
        SensorSample raw = TestSamples.With(
            gyro: new Vector3(11, 22, 33),
            accel: new Vector3(0.6, 0, -1),
            mag: new Vector3(15, 25, 45));

        SensorSample corrected = profile.Apply(raw);

        corrected.Gyroscope.Should().Be(new Vector3(10, 20, 30));
        corrected.Accelerometer.Should().Be(new Vector3(1.0, 0, -1));
        corrected.Magnetometer.Should().Be(new Vector3(10, 20, 20));
        corrected.Timestamp.Should().Be(raw.Timestamp);
        corrected.TemperatureCelsius.Should().Be(raw.TemperatureCelsius);
    }

    [Fact]
    public void Identity_profile_is_identity()
    {
        CalibrationProfile.Identity.IsIdentity.Should().BeTrue();
        new CalibrationProfile { GyroscopeBias = new Vector3(0.1, 0, 0) }.IsIdentity.Should().BeFalse();

        SensorSample sample = TestSamples.Level();
        CalibrationProfile.Identity.Apply(sample).Should().Be(sample);
    }

    [Fact]
    public void Service_loads_the_persisted_profile_on_construction()
    {
        var persisted = new CalibrationProfile { GyroscopeBias = new Vector3(1, 1, 1) };
        var service = new CalibrationService(new FakeStore { Stored = persisted });

        service.CurrentProfile.Should().Be(persisted);
    }

    [Fact]
    public void Service_applies_persists_and_notifies()
    {
        var store = new FakeStore();
        var service = new CalibrationService(store);
        var profile = new CalibrationProfile { GyroscopeBias = new Vector3(2, 0, 0) };
        int notifications = 0;
        service.ProfileChanged += (_, _) => notifications++;

        service.ApplyProfile(profile);

        service.CurrentProfile.Should().Be(profile);
        store.Stored.Should().Be(profile);
        notifications.Should().Be(1);
        service.Correct(TestSamples.With(gyro: new Vector3(3, 0, 0)))
            .Gyroscope.X.Should().Be(1);
    }

    [Fact]
    public void A_corrupt_persisted_profile_falls_back_to_identity()
    {
        var service = new CalibrationService(new FakeStore { LoadException = new FormatException("corrupt json") });
        service.CurrentProfile.Should().Be(CalibrationProfile.Identity);
    }

    [Fact]
    public void A_failed_save_keeps_the_profile_active_but_surfaces_the_problem()
    {
        var store = new FakeStore { SaveException = new IOException("disk full") };
        var service = new CalibrationService(store);
        var profile = new CalibrationProfile { GyroscopeBias = new Vector3(1, 0, 0) };

        Action act = () => service.ApplyProfile(profile);

        act.Should().Throw<CalibrationException>().WithMessage("*could not be saved*");
        service.CurrentProfile.Should().Be(profile, "the in-memory apply must not be lost");
    }

    [Fact]
    public void ResetToIdentity_reverts_and_persists()
    {
        var store = new FakeStore();
        var service = new CalibrationService(store);
        service.ApplyProfile(new CalibrationProfile { GyroscopeBias = new Vector3(1, 0, 0) });

        service.ResetToIdentity();

        service.CurrentProfile.IsIdentity.Should().BeTrue();
        store.Stored!.IsIdentity.Should().BeTrue();
    }

    private sealed class FakeStore : ICalibrationProfileStore
    {
        internal CalibrationProfile? Stored { get; set; }

        internal Exception? LoadException { get; init; }

        internal Exception? SaveException { get; init; }

        public CalibrationProfile? Load() => LoadException is null ? Stored : throw LoadException;

        public void Save(CalibrationProfile profile)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            Stored = profile;
        }
    }
}
