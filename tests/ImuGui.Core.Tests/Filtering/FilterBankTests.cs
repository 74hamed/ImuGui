using FluentAssertions;
using ImuGui.Core.Filtering;
using ImuGui.Core.Models;
using Xunit;

namespace ImuGui.Core.Tests.Filtering;

public class FilterBankTests
{
    [Fact]
    public void Filters_every_channel_independently_toward_its_own_value()
    {
        FilterBank bank = FilterBank.CreateKalman();
        SensorSample input = SensorSample.FromChannelValues(
            TimeSpan.FromMilliseconds(20), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

        SensorSample filtered = input;
        for (int i = 0; i < 300; i++)
        {
            filtered = bank.Process(input);
        }

        foreach (SensorChannel channel in SensorChannels.All)
        {
            filtered.GetChannelValue(channel)
                .Should().BeApproximately(input.GetChannelValue(channel), 0.01,
                    $"channel {channel} converges to its own input");
        }

        filtered.Timestamp.Should().Be(input.Timestamp, "filtering never alters timestamps");
    }

    [Fact]
    public void A_quiet_channel_is_unaffected_by_a_loud_neighbor()
    {
        FilterBank bank = FilterBank.CreateKalman();
        SensorSample input = SensorSample.FromChannelValues(
            TimeSpan.Zero, [0, 500, 0, 0, 0, 0, 0, 0, 0, 0]);

        SensorSample filtered = bank.Process(input);

        filtered.GetChannelValue(SensorChannel.GyroscopeX).Should().Be(0);
        filtered.GetChannelValue(SensorChannel.GyroscopeY).Should().NotBe(0);
    }

    [Fact]
    public void RetuneAll_applies_to_every_channel_and_updates_the_current_config()
    {
        FilterBank bank = FilterBank.CreateKalman();
        var newConfig = new FilterConfig { ProcessNoise = 0.02, MeasurementNoise = 0.7 };

        bank.RetuneAll(newConfig, RetuneBehavior.PreserveState);

        bank.CurrentConfig.Should().Be(newConfig);
        foreach (SensorChannel channel in SensorChannels.All)
        {
            bank.GetFilter(channel).Config.Should().Be(newConfig);
        }
    }

    [Fact]
    public void ResetAll_restarts_every_channel()
    {
        FilterBank bank = FilterBank.CreateKalman();
        SensorSample input = SensorSample.FromChannelValues(
            TimeSpan.Zero, [9, 9, 9, 9, 9, 9, 9, 9, 9, 9]);
        for (int i = 0; i < 50; i++)
        {
            bank.Process(input);
        }

        bank.ResetAll();

        bank.GetFilter(SensorChannel.GyroscopeX).Value.Should().Be(0);
    }

    [Fact]
    public void Invalid_retune_parameters_are_rejected_before_touching_any_filter()
    {
        FilterBank bank = FilterBank.CreateKalman();
        FilterConfig before = bank.CurrentConfig;

        Action act = () => bank.RetuneAll(
            new FilterConfig { MeasurementNoise = 0 }, RetuneBehavior.ResetState);

        act.Should().Throw<ArgumentOutOfRangeException>();
        bank.CurrentConfig.Should().Be(before);
    }
}
