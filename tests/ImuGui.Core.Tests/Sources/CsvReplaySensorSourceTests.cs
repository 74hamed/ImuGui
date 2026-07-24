using FluentAssertions;
using ImuGui.Core.Models;
using ImuGui.Core.Sources;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Sources;

public class CsvReplaySensorSourceTests
{
    private const string Header = "GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature";

    private static string Row(double seed) =>
        string.Join(',', Enumerable.Range(0, 10).Select(i => (seed + i).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public async Task Replays_all_rows_in_order_with_scheduled_timestamps()
    {
        using var file = new TempCsvFile(Header, Row(1), Row(2), Row(3));
        var clock = new FakeClock();
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path, ReplayRateHz = 100, Loop = false }, clock);
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForStateAsync(SensorConnectionState.Disconnected);

        source.LoadedSampleCount.Should().Be(3);
        source.MalformedRowCount.Should().Be(0);

        SensorSample[] samples = probe.Samples.ToArray();
        samples.Should().HaveCount(3);
        samples[0].Gyroscope.X.Should().Be(1);
        samples[1].Gyroscope.X.Should().Be(2);
        samples[2].Gyroscope.X.Should().Be(3);

        // Scheduled pacing: sample i is stamped at i / rate.
        (samples[1].Timestamp - samples[0].Timestamp).Should().Be(TimeSpan.FromMilliseconds(10));
        (samples[2].Timestamp - samples[1].Timestamp).Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task Loops_with_monotonically_increasing_timestamps_when_enabled()
    {
        using var file = new TempCsvFile(Header, Row(1), Row(2), Row(3));
        var clock = new FakeClock();
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path, ReplayRateHz = 200, Loop = true }, clock);
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForSamplesAsync(8);
        await source.StopAsync();

        SensorSample[] samples = probe.Samples.ToArray();
        samples.Length.Should().BeGreaterThanOrEqualTo(8, "three rows must wrap around");
        samples.Zip(samples.Skip(1))
            .All(pair => pair.Second.Timestamp > pair.First.Timestamp)
            .Should().BeTrue("timestamps keep increasing across the loop boundary");
    }

    [Fact]
    public async Task Missing_file_fails_with_an_actionable_message_and_stays_disconnected()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.csv");
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = missingPath }, new FakeClock());

        Func<Task> act = () => source.StartAsync();

        (await act.Should().ThrowAsync<SensorSourceException>())
            .WithMessage($"*{missingPath}*");
        source.ConnectionState.Should().Be(SensorConnectionState.Disconnected);
    }

    [Fact]
    public async Task Malformed_rows_are_skipped_and_counted_not_zero_filled()
    {
        using var file = new TempCsvFile(
            Header,
            Row(1),
            "1,2,3",                       // too few fields
            "a,b,c,d,e,f,g,h,i,j",         // non-numeric
            Row(2));
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path, ReplayRateHz = 500, Loop = false }, new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForStateAsync(SensorConnectionState.Disconnected);

        source.LoadedSampleCount.Should().Be(2);
        source.MalformedRowCount.Should().Be(2);
        probe.Samples.Select(s => s.Gyroscope.X).Should().Equal(1, 2);
    }

    [Fact]
    public async Task A_file_with_no_valid_rows_fails_and_explains_the_expected_format()
    {
        using var file = new TempCsvFile("0.1;0.2;0.3;0.4;0.5;0.6;0.7;0.8;0.9;25");
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path }, new FakeClock());

        Func<Task> act = () => source.StartAsync();

        (await act.Should().ThrowAsync<SensorSourceException>())
            .WithMessage("*GyroX,GyroY,GyroZ*");
    }

    [Fact]
    public async Task Headerless_files_load_via_column_positions()
    {
        using var file = new TempCsvFile(Row(5), Row(6));
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path, ReplayRateHz = 500, Loop = false }, new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForStateAsync(SensorConnectionState.Disconnected);

        source.LoadedSampleCount.Should().Be(2);
        probe.Samples.First().Gyroscope.X.Should().Be(5);
    }

    [Fact]
    public void Non_positive_replay_rate_is_rejected_at_construction()
    {
        Action act = () => _ = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = "any.csv", ReplayRateHz = 0 }, new FakeClock());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Source_can_be_restarted_after_stopping()
    {
        using var file = new TempCsvFile(Header, Row(1), Row(2));
        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = file.Path, ReplayRateHz = 200, Loop = true }, new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForSamplesAsync(2);
        await source.StopAsync();
        source.ConnectionState.Should().Be(SensorConnectionState.Disconnected);

        await source.StartAsync();
        source.ConnectionState.Should().Be(SensorConnectionState.Connected);
        await probe.WaitForSamplesAsync(2);
        await source.StopAsync();
    }

    [Fact]
    public async Task Bundled_sample_recording_loads_cleanly_and_matches_conventions()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "samples", "imu-sample.csv");
        File.Exists(fixturePath).Should().BeTrue("the sample CSV is copied next to the test binaries");

        using var source = new CsvReplaySensorSource(
            new CsvReplayOptions { FilePath = fixturePath, ReplayRateHz = 5000, Loop = false },
            new FakeClock());
        using var probe = new SourceProbe(source);

        await source.StartAsync();
        await probe.WaitForStateAsync(SensorConnectionState.Disconnected, TimeSpan.FromSeconds(30));

        source.LoadedSampleCount.Should().Be(2000, "40 s at 50 Hz");
        source.MalformedRowCount.Should().Be(0);

        // The recording starts level & stationary: specific force ≈ (0, 0, −1) g.
        SensorSample first = probe.Samples.First();
        first.Accelerometer.X.Should().BeApproximately(0, 0.05);
        first.Accelerometer.Y.Should().BeApproximately(0, 0.05);
        first.Accelerometer.Z.Should().BeApproximately(-1, 0.05);
    }
}
