using FluentAssertions;
using ImuGui.Core.Models;
using ImuGui.Core.Sources;
using Xunit;

namespace ImuGui.Core.Tests.Sources;

public class SensorLineParserTests
{
    [Fact]
    public void Parses_a_valid_line_into_channel_values()
    {
        const string line = "0.1,-0.2,0.3, 1.5 ,2.5,3.5,10,20,30,25.4";

        bool ok = SensorLineParser.TryParse(line, out SensorSample? sample, out string? error);

        ok.Should().BeTrue(error);
        sample!.Gyroscope.Should().Be(new Vector3(0.1, -0.2, 0.3));
        sample.Accelerometer.Should().Be(new Vector3(1.5, 2.5, 3.5));
        sample.Magnetometer.Should().Be(new Vector3(10, 20, 30));
        sample.TemperatureCelsius.Should().Be(25.4);
        sample.Timestamp.Should().Be(TimeSpan.Zero, "the source stamps arrival time");
    }

    [Fact]
    public void Parses_scientific_notation_and_signs()
    {
        const string line = "1e-3,-2E2,+0.5,0,0,-1,0,0,0,2.5e1";

        SensorLineParser.TryParse(line, out SensorSample? sample, out _).Should().BeTrue();
        sample!.Gyroscope.X.Should().Be(0.001);
        sample.Gyroscope.Y.Should().Be(-200);
        sample.TemperatureCelsius.Should().Be(25);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_input(string? line)
    {
        SensorLineParser.TryParse(line, out _, out string? error).Should().BeFalse();
        error.Should().Contain("Empty");
    }

    [Theory]
    [InlineData("1,2,3", 3)]
    [InlineData("1,2,3,4,5,6,7,8,9,10,11", 11)]
    public void Rejects_wrong_field_count_with_the_count_in_the_message(string line, int actualCount)
    {
        SensorLineParser.TryParse(line, out _, out string? error).Should().BeFalse();
        error.Should().Contain("10").And.Contain(actualCount.ToString());
    }

    [Fact]
    public void Rejects_non_numeric_fields_and_names_the_offender()
    {
        const string line = "0.1,banana,0.3,0,0,-1,0,0,0,25";

        SensorLineParser.TryParse(line, out _, out string? error).Should().BeFalse();
        error.Should().Contain("Field 2").And.Contain("banana");
    }

    [Fact]
    public void European_decimal_commas_shift_the_field_count_and_are_rejected()
    {
        // "0,1" per value doubles the separators — a clear, loud failure instead of silent garbage.
        const string line = "0,1,0,2,0,3,0,4,0,5,0,6,0,7,0,8,0,9,25,0";

        SensorLineParser.TryParse(line, out _, out string? error).Should().BeFalse();
        error.Should().Contain("Expected 10");
    }

    [Theory]
    [InlineData("GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature", true)]
    [InlineData("gyrox,gyroy", true)]
    [InlineData("0.5,1,2,3,4,5,6,7,8,9", false)]
    [InlineData("-12.5,...", false)]
    public void Header_detection_keys_off_the_first_field(string line, bool expected) =>
        SensorLineParser.IsLikelyHeader(line).Should().Be(expected);
}
