using FluentAssertions;
using ImuGui.Core.Calibration;
using ImuGui.Core.Filtering;
using ImuGui.Core.Fusion;
using ImuGui.Core.Models;
using ImuGui.Core.Pipeline;
using ImuGui.Core.Sources;
using ImuGui.Core.Tests.TestUtilities;
using Xunit;

namespace ImuGui.Core.Tests.Pipeline;

public class SensorPipelineTests
{
    private static SensorPipeline CreatePipeline(ICalibrationService? calibration = null) => new(
        FilterBank.CreateKalman(),
        new MahonyOrientationEstimator(),
        new MahonyOrientationEstimator(),
        calibration ?? new CalibrationService());

    [Fact]
    public void Delta_time_is_measured_from_sample_timestamps()
    {
        using SensorPipeline pipeline = CreatePipeline();

        ProcessedFrame first = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(100)));
        ProcessedFrame second = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(120)));
        ProcessedFrame third = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(155)));

        first.DeltaTime.Should().Be(TimeSpan.Zero, "there is no previous sample");
        second.DeltaTime.Should().Be(TimeSpan.FromMilliseconds(20));
        third.DeltaTime.Should().Be(TimeSpan.FromMilliseconds(35));
    }

    [Fact]
    public void Non_monotonic_timestamps_yield_zero_dt_not_negative()
    {
        using SensorPipeline pipeline = CreatePipeline();
        pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(100)));

        ProcessedFrame frame = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(90)));

        frame.DeltaTime.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Frames_carry_both_raw_and_filtered_variants()
    {
        using SensorPipeline pipeline = CreatePipeline();

        ProcessedFrame frame = pipeline.Process(TestSamples.Level());

        frame.RawSample.Accelerometer.Z.Should().Be(-1, "raw means unfiltered");
        frame.FilteredSample.Accelerometer.Z.Should().BeGreaterThan(-1)
            .And.BeLessThan(0, "the filter is still converging from its initial estimate");
        double.IsFinite(frame.RawOrientation.RollDegrees).Should().BeTrue();
        double.IsFinite(frame.FilteredOrientation.RollDegrees).Should().BeTrue();
    }

    [Fact]
    public void Calibration_toggle_switches_correction_on_and_off()
    {
        var calibration = new CalibrationService();
        calibration.ApplyProfile(new CalibrationProfile { GyroscopeBias = new Vector3(1, 2, 3) });
        using SensorPipeline pipeline = CreatePipeline(calibration);

        pipeline.CalibrationEnabled = true;
        ProcessedFrame corrected = pipeline.Process(
            TestSamples.With(gyro: new Vector3(1, 2, 3), timestamp: TimeSpan.FromMilliseconds(10)));
        corrected.RawSample.Gyroscope.Should().Be(Vector3.Zero);

        pipeline.CalibrationEnabled = false;
        ProcessedFrame uncorrected = pipeline.Process(
            TestSamples.With(gyro: new Vector3(1, 2, 3), timestamp: TimeSpan.FromMilliseconds(20)));
        uncorrected.RawSample.Gyroscope.Should().Be(new Vector3(1, 2, 3));
    }

    [Fact]
    public void Latest_frame_count_and_event_stay_consistent()
    {
        using SensorPipeline pipeline = CreatePipeline();
        var receivedFrames = new List<ProcessedFrame>();
        pipeline.FrameProcessed += (_, e) => receivedFrames.Add(e.Frame);

        pipeline.LatestFrame.Should().BeNull();
        ProcessedFrame last = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(10)));
        last = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(20)));

        pipeline.FrameCount.Should().Be(2);
        pipeline.LatestFrame.Should().BeSameAs(last);
        receivedFrames.Should().HaveCount(2);
    }

    [Fact]
    public void Attach_consumes_source_samples_and_detach_stops()
    {
        using SensorPipeline pipeline = CreatePipeline();
        using var source = new StubSensorSource();

        pipeline.AttachSource(source);
        source.Raise(TestSamples.Level(TimeSpan.FromMilliseconds(1)));
        pipeline.FrameCount.Should().Be(1);

        pipeline.DetachSource();
        source.Raise(TestSamples.Level(TimeSpan.FromMilliseconds(2)));
        pipeline.FrameCount.Should().Be(1, "a detached pipeline ignores the source");
    }

    [Fact]
    public void Reset_clears_state_for_a_fresh_run()
    {
        using SensorPipeline pipeline = CreatePipeline();
        pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(10)));

        pipeline.Reset();

        pipeline.LatestFrame.Should().BeNull();
        pipeline.FrameCount.Should().Be(0);
        ProcessedFrame frame = pipeline.Process(TestSamples.Level(TimeSpan.FromMilliseconds(500)));
        frame.DeltaTime.Should().Be(TimeSpan.Zero, "dt tracking restarted");
    }

    [Fact]
    public void Estimators_can_be_swapped_at_runtime_and_reinitialize()
    {
        using SensorPipeline pipeline = CreatePipeline();
        pipeline.Process(TestSamples.FromAttitude(0.4, 0.2, 1.0, TimeSpan.FromMilliseconds(10)));

        pipeline.ReplaceEstimators(
            new ComplementaryOrientationEstimator(), new ComplementaryOrientationEstimator());

        ProcessedFrame frame = pipeline.Process(
            TestSamples.FromAttitude(AngleMath.DegreesToRadians(20), 0, 0, TimeSpan.FromMilliseconds(30)));
        frame.RawOrientation.RollDegrees.Should().BeApproximately(20, 1e-6,
            "the fresh estimator re-initializes from the next sample");

        Action act = () =>
        {
            var estimator = new MahonyOrientationEstimator();
            pipeline.ReplaceEstimators(estimator, estimator);
        };
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sharing_one_estimator_instance_between_paths_is_rejected()
    {
        var estimator = new MahonyOrientationEstimator();

        Action act = () => _ = new SensorPipeline(
            FilterBank.CreateKalman(), estimator, estimator, new CalibrationService());

        act.Should().Throw<ArgumentException>();
    }

    private sealed class StubSensorSource : ISensorSource
    {
        public event EventHandler<SensorSampleEventArgs>? SampleReceived;

        public event EventHandler<SensorConnectionStateChangedEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<SensorSourceErrorEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public string DisplayName => "Stub";

        public SensorConnectionState ConnectionState => SensorConnectionState.Connected;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }

        internal void Raise(SensorSample sample) =>
            SampleReceived?.Invoke(this, new SensorSampleEventArgs(sample));
    }
}
