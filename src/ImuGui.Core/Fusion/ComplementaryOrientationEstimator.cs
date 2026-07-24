using ImuGui.Core.Models;

namespace ImuGui.Core.Fusion;

/// <summary>
/// Euler-angle complementary filter (selectable alternative to the Mahony default).
/// Gyroscope rates are propagated through proper Euler-rate kinematics and blended with
/// accelerometer roll/pitch and the tilt-compensated magnetometer heading using a
/// dt-aware coefficient α = τ / (τ + dt). Subject to gimbal ambiguity near ±90° pitch,
/// which is why it is not the default.
/// </summary>
public sealed class ComplementaryOrientationEstimator : IOrientationEstimator
{
    private const double MaxDeltaSeconds = 0.5;
    private const double VectorEpsilonSquared = 1e-12;
    private const double MinCosPitch = 0.01;

    private readonly ComplementaryOptions _options;
    private double _rollRadians;
    private double _pitchRadians;
    private double _yawRadians;
    private bool _initialized;

    /// <summary>Creates the estimator.</summary>
    /// <param name="options">Tuning; defaults when null.</param>
    public ComplementaryOrientationEstimator(ComplementaryOptions? options = null)
    {
        _options = options ?? new ComplementaryOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public Orientation CurrentOrientation => new(
        AngleMath.RadiansToDegrees(_rollRadians),
        AngleMath.RadiansToDegrees(_pitchRadians),
        AngleMath.WrapDegreesPositive(AngleMath.RadiansToDegrees(_yawRadians)));

    /// <inheritdoc />
    public Quaternion CurrentAttitude =>
        Quaternion.FromEulerAngles(_rollRadians, _pitchRadians, _yawRadians);

    /// <inheritdoc />
    public void Update(SensorSample sample, TimeSpan deltaTime)
    {
        ArgumentNullException.ThrowIfNull(sample);

        bool accelUsable = sample.Accelerometer.MagnitudeSquared > VectorEpsilonSquared;
        bool magUsable = sample.Magnetometer.MagnitudeSquared > VectorEpsilonSquared;

        if (!_initialized)
        {
            _rollRadians = accelUsable ? TiltCompensation.ComputeRollRadians(sample.Accelerometer) : 0;
            _pitchRadians = accelUsable ? TiltCompensation.ComputePitchRadians(sample.Accelerometer) : 0;
            _yawRadians = magUsable
                ? TiltCompensation.ComputeHeadingRadians(sample.Magnetometer, _rollRadians, _pitchRadians)
                : 0;
            _initialized = true;
            return;
        }

        double dt = Math.Min(deltaTime.TotalSeconds, MaxDeltaSeconds);
        if (dt <= 0)
        {
            return;
        }

        // Propagate gyro body rates through Euler-rate kinematics.
        double p = AngleMath.DegreesToRadians(sample.Gyroscope.X);
        double q = AngleMath.DegreesToRadians(sample.Gyroscope.Y);
        double r = AngleMath.DegreesToRadians(sample.Gyroscope.Z);

        double sinRoll = Math.Sin(_rollRadians);
        double cosRoll = Math.Cos(_rollRadians);
        double cosPitch = Math.Cos(_pitchRadians);
        double safeCosPitch = Math.Sign(cosPitch) is 0 ? MinCosPitch : Math.Max(Math.Abs(cosPitch), MinCosPitch) * Math.Sign(cosPitch);
        double tanPitch = Math.Sin(_pitchRadians) / safeCosPitch;

        double predictedRoll = _rollRadians + (dt * (p + (q * sinRoll * tanPitch) + (r * cosRoll * tanPitch)));
        double predictedPitch = _pitchRadians + (dt * ((q * cosRoll) - (r * sinRoll)));
        double predictedYaw = _yawRadians + (dt * (((q * sinRoll) + (r * cosRoll)) / safeCosPitch));

        double alpha = _options.TimeConstantSeconds / (_options.TimeConstantSeconds + dt);

        if (accelUsable)
        {
            double measuredRoll = TiltCompensation.ComputeRollRadians(sample.Accelerometer);
            double measuredPitch = TiltCompensation.ComputePitchRadians(sample.Accelerometer);
            _rollRadians = BlendWrapped(predictedRoll, measuredRoll, alpha);
            _pitchRadians = (alpha * predictedPitch) + ((1 - alpha) * measuredPitch);
        }
        else
        {
            _rollRadians = AngleMath.WrapRadiansSigned(predictedRoll);
            _pitchRadians = predictedPitch;
        }

        if (magUsable)
        {
            double measuredYaw = TiltCompensation.ComputeHeadingRadians(
                sample.Magnetometer, _rollRadians, _pitchRadians);
            _yawRadians = BlendWrapped(predictedYaw, measuredYaw, alpha);
        }
        else
        {
            _yawRadians = AngleMath.WrapRadiansSigned(predictedYaw);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _rollRadians = 0;
        _pitchRadians = 0;
        _yawRadians = 0;
        _initialized = false;
    }

    /// <summary>Wrap-aware blend: measured + α · shortest(predicted − measured).</summary>
    private static double BlendWrapped(double predicted, double measured, double alpha) =>
        AngleMath.WrapRadiansSigned(
            measured + (alpha * AngleMath.WrapRadiansSigned(predicted - measured)));
}
