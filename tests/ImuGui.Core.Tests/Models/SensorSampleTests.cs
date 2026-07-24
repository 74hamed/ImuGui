using FluentAssertions;
using ImuGui.Core.Models;
using Xunit;

namespace ImuGui.Core.Tests.Models;

public class SensorSampleTests
{
    [Fact]
    public void Channel_values_round_trip_through_the_enum_order()
    {
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        SensorSample sample = SensorSample.FromChannelValues(TimeSpan.FromSeconds(1), values);

        sample.Gyroscope.Should().Be(new Vector3(1, 2, 3));
        sample.Accelerometer.Should().Be(new Vector3(4, 5, 6));
        sample.Magnetometer.Should().Be(new Vector3(7, 8, 9));
        sample.TemperatureCelsius.Should().Be(10);

        foreach (SensorChannel channel in SensorChannels.All)
        {
            sample.GetChannelValue(channel).Should().Be(values[(int)channel]);
        }
    }

    [Fact]
    public void FromChannelValues_rejects_wrong_count()
    {
        Action act = () => SensorSample.FromChannelValues(TimeSpan.Zero, [1, 2, 3]);
        act.Should().Throw<ArgumentException>().WithMessage("*Expected 10*got 3*");
    }

    [Fact]
    public void SensorChannels_enumerates_all_ten_channels_in_column_order()
    {
        SensorChannels.All.Should().HaveCount(SensorChannels.Count);
        SensorChannels.All[0].Should().Be(SensorChannel.GyroscopeX);
        SensorChannels.All[9].Should().Be(SensorChannel.Temperature);
    }
}
