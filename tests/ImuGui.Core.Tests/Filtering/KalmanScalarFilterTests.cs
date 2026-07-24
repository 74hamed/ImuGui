using FluentAssertions;
using ImuGui.Core.Filtering;
using Xunit;

namespace ImuGui.Core.Tests.Filtering;

public class KalmanScalarFilterTests
{
    [Fact]
    public void Converges_to_a_constant_input()
    {
        var filter = new KalmanScalarFilter(FilterConfig.Default);

        double output = 0;
        for (int i = 0; i < 200; i++)
        {
            output = filter.Update(5.0);
        }

        output.Should().BeApproximately(5.0, 0.01);
        filter.Value.Should().Be(output);
    }

    [Fact]
    public void Higher_measurement_noise_R_produces_a_smoother_output()
    {
        var jittery = new KalmanScalarFilter(new FilterConfig { ProcessNoise = 0.001, MeasurementNoise = 0.01 });
        var smooth = new KalmanScalarFilter(new FilterConfig { ProcessNoise = 0.001, MeasurementNoise = 5.0 });

        var random = new Random(1);
        double jitteryTravel = 0;
        double smoothTravel = 0;
        double previousJittery = 0;
        double previousSmooth = 0;

        for (int i = 0; i < 500; i++)
        {
            double noisy = 5.0 + (random.NextDouble() - 0.5);
            double a = jittery.Update(noisy);
            double b = smooth.Update(noisy);
            if (i >= 100)
            {
                jitteryTravel += Math.Abs(a - previousJittery);
                smoothTravel += Math.Abs(b - previousSmooth);
            }

            previousJittery = a;
            previousSmooth = b;
        }

        smoothTravel.Should().BeLessThan(jitteryTravel / 2, "large R discounts each measurement");
    }

    [Fact]
    public void Higher_process_noise_Q_tracks_a_step_faster()
    {
        var sluggish = new KalmanScalarFilter(new FilterConfig { ProcessNoise = 0.001, MeasurementNoise = 1.0 });
        var agile = new KalmanScalarFilter(new FilterConfig { ProcessNoise = 0.5, MeasurementNoise = 1.0 });

        for (int i = 0; i < 50; i++)
        {
            sluggish.Update(0);
            agile.Update(0);
        }

        double sluggishOutput = 0;
        double agileOutput = 0;
        for (int i = 0; i < 10; i++)
        {
            sluggishOutput = sluggish.Update(10);
            agileOutput = agile.Update(10);
        }

        agileOutput.Should().BeGreaterThan(sluggishOutput + 1, "large Q lets the state move quickly");
    }

    [Fact]
    public void Retune_with_ResetState_restarts_estimation_and_PreserveState_keeps_it()
    {
        var resetFilter = new KalmanScalarFilter(FilterConfig.Default);
        var preserveFilter = new KalmanScalarFilter(FilterConfig.Default);
        for (int i = 0; i < 100; i++)
        {
            resetFilter.Update(7);
            preserveFilter.Update(7);
        }

        var newConfig = new FilterConfig { ProcessNoise = 0.01, MeasurementNoise = 0.5 };
        resetFilter.Retune(newConfig, RetuneBehavior.ResetState);
        preserveFilter.Retune(newConfig, RetuneBehavior.PreserveState);

        resetFilter.Value.Should().Be(newConfig.InitialEstimate, "ResetState restarts from X₀");
        preserveFilter.Value.Should().BeApproximately(7, 0.05, "PreserveState keeps the estimate");
        resetFilter.Config.Should().Be(newConfig);
        preserveFilter.Config.Should().Be(newConfig);
    }

    [Fact]
    public void Reset_returns_to_the_configured_initial_state()
    {
        var config = new FilterConfig { InitialEstimate = 2.5 };
        var filter = new KalmanScalarFilter(config);
        filter.Update(100);

        filter.Reset();

        filter.Value.Should().Be(2.5);
    }

    [Theory]
    [InlineData(-0.1, 1, 1, 0)]      // negative Q
    [InlineData(0.1, 0, 1, 0)]       // zero R
    [InlineData(0.1, -1, 1, 0)]      // negative R
    [InlineData(0.1, 1, -1, 0)]      // negative P
    [InlineData(double.NaN, 1, 1, 0)]
    public void Invalid_configurations_are_rejected(double q, double r, double p, double x)
    {
        var config = new FilterConfig
        {
            ProcessNoise = q,
            MeasurementNoise = r,
            InitialCovariance = p,
            InitialEstimate = x,
        };

        Action act = () => _ = new KalmanScalarFilter(config);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
