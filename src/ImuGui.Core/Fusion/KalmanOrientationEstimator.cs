using ImuGui.Core.Models;

namespace ImuGui.Core.Fusion;

/// <summary>
/// Per-axis Kalman attitude filter (the classic two-state formulation): each Euler angle
/// carries a state vector [angle, gyroBias]. The prediction integrates gyro rates — passed
/// through proper Euler-rate kinematics and with the measured dt — while accelerometer
/// roll/pitch and the tilt-compensated magnetometer heading provide the corrections.
/// <para>
/// Its distinguishing feature over the complementary filter is the explicit gyro-bias
/// state: a constant rate offset is learned online and removed. Like all Euler-based
/// strategies it has gimbal ambiguity near ±90° pitch, which is why the quaternion Mahony
/// filter remains the default.
/// </para>
/// </summary>
public sealed class KalmanOrientationEstimator : IOrientationEstimator
{
    private const double MaxDeltaSeconds = 0.5;
    private const double VectorEpsilonSquared = 1e-12;
    private const double MinCosPitch = 0.01;

    private readonly AxisAttitudeKalman _rollFilter;
    private readonly AxisAttitudeKalman _pitchFilter;
    private readonly AxisAttitudeKalman _yawFilter;
    private bool _initialized;

    /// <summary>Creates the estimator.</summary>
    /// <param name="options">Tuning; defaults when null.</param>
    public KalmanOrientationEstimator(KalmanEstimatorOptions? options = null)
    {
        KalmanEstimatorOptions effective = options ?? new KalmanEstimatorOptions();
        effective.Validate();
        _rollFilter = new AxisAttitudeKalman(effective);
        _pitchFilter = new AxisAttitudeKalman(effective);
        _yawFilter = new AxisAttitudeKalman(effective);
    }

    /// <inheritdoc />
    public Orientation CurrentOrientation => new(
        AngleMath.RadiansToDegrees(_rollFilter.Angle),
        AngleMath.RadiansToDegrees(_pitchFilter.Angle),
        AngleMath.WrapDegreesPositive(AngleMath.RadiansToDegrees(_yawFilter.Angle)));

    /// <inheritdoc />
    public Quaternion CurrentAttitude =>
        Quaternion.FromEulerAngles(_rollFilter.Angle, _pitchFilter.Angle, _yawFilter.Angle);

    /// <inheritdoc />
    public void Update(SensorSample sample, TimeSpan deltaTime)
    {
        ArgumentNullException.ThrowIfNull(sample);

        bool accelUsable = sample.Accelerometer.MagnitudeSquared > VectorEpsilonSquared;
        bool magUsable = sample.Magnetometer.MagnitudeSquared > VectorEpsilonSquared;

        if (!_initialized)
        {
            double initialRoll = accelUsable ? TiltCompensation.ComputeRollRadians(sample.Accelerometer) : 0;
            double initialPitch = accelUsable ? TiltCompensation.ComputePitchRadians(sample.Accelerometer) : 0;
            double initialYaw = magUsable
                ? TiltCompensation.ComputeHeadingRadians(sample.Magnetometer, initialRoll, initialPitch)
                : 0;
            _rollFilter.Initialize(initialRoll);
            _pitchFilter.Initialize(initialPitch);
            _yawFilter.Initialize(initialYaw);
            _initialized = true;
            return;
        }

        double dt = Math.Min(deltaTime.TotalSeconds, MaxDeltaSeconds);
        if (dt <= 0)
        {
            return;
        }

        // Predict: body rates → Euler rates through the kinematic map, then integrate
        // each axis with its bias-corrected rate.
        double p = AngleMath.DegreesToRadians(sample.Gyroscope.X);
        double q = AngleMath.DegreesToRadians(sample.Gyroscope.Y);
        double r = AngleMath.DegreesToRadians(sample.Gyroscope.Z);

        double sinRoll = Math.Sin(_rollFilter.Angle);
        double cosRoll = Math.Cos(_rollFilter.Angle);
        double cosPitch = Math.Cos(_pitchFilter.Angle);
        double safeCosPitch = Math.Sign(cosPitch) is 0
            ? MinCosPitch
            : Math.Max(Math.Abs(cosPitch), MinCosPitch) * Math.Sign(cosPitch);
        double tanPitch = Math.Sin(_pitchFilter.Angle) / safeCosPitch;

        double rollRate = p + (q * sinRoll * tanPitch) + (r * cosRoll * tanPitch);
        double pitchRate = (q * cosRoll) - (r * sinRoll);
        double yawRate = ((q * sinRoll) + (r * cosRoll)) / safeCosPitch;

        _rollFilter.Predict(rollRate, dt);
        _pitchFilter.Predict(pitchRate, dt);
        _yawFilter.Predict(yawRate, dt);

        // Correct: wrap-aware innovations from the vector observations.
        if (accelUsable)
        {
            double measuredRoll = TiltCompensation.ComputeRollRadians(sample.Accelerometer);
            double measuredPitch = TiltCompensation.ComputePitchRadians(sample.Accelerometer);
            _rollFilter.Correct(AngleMath.WrapRadiansSigned(measuredRoll - _rollFilter.Angle));
            _pitchFilter.Correct(measuredPitch - _pitchFilter.Angle);
        }

        if (magUsable)
        {
            double measuredYaw = TiltCompensation.ComputeHeadingRadians(
                sample.Magnetometer, _rollFilter.Angle, _pitchFilter.Angle);
            _yawFilter.Correct(AngleMath.WrapRadiansSigned(measuredYaw - _yawFilter.Angle));
        }

        _rollFilter.WrapAngle();
        _yawFilter.WrapAngle();
    }

    /// <inheritdoc />
    public void Reset()
    {
        _rollFilter.Initialize(0);
        _pitchFilter.Initialize(0);
        _yawFilter.Initialize(0);
        _initialized = false;
    }

    /// <summary>
    /// One angle's two-state Kalman filter: x = [angle, gyroBias], F = [[1, −dt], [0, 1]],
    /// observation z = angle. The textbook formulation used across attitude estimation.
    /// </summary>
    private sealed class AxisAttitudeKalman
    {
        private readonly KalmanEstimatorOptions _options;
        private double _p00 = 1;
        private double _p01;
        private double _p10;
        private double _p11 = 1;

        internal AxisAttitudeKalman(KalmanEstimatorOptions options) => _options = options;

        internal double Angle { get; private set; }

        internal double Bias { get; private set; }

        internal void Initialize(double angleRadians)
        {
            Angle = angleRadians;
            Bias = 0;
            _p00 = 1;
            _p01 = 0;
            _p10 = 0;
            _p11 = 1;
        }

        internal void Predict(double rateRadiansPerSecond, double dt)
        {
            Angle += dt * (rateRadiansPerSecond - Bias);

            _p00 += dt * ((dt * _p11) - _p01 - _p10 + _options.AngleProcessNoise);
            _p01 -= dt * _p11;
            _p10 -= dt * _p11;
            _p11 += _options.BiasProcessNoise * dt;
        }

        internal void Correct(double innovationRadians)
        {
            double s = _p00 + _options.MeasurementNoise;
            double k0 = _p00 / s;
            double k1 = _p10 / s;

            Angle += k0 * innovationRadians;
            Bias += k1 * innovationRadians;

            double p00 = _p00;
            double p01 = _p01;
            _p00 -= k0 * p00;
            _p01 -= k0 * p01;
            _p10 -= k1 * p00;
            _p11 -= k1 * p01;
        }

        internal void WrapAngle() => Angle = AngleMath.WrapRadiansSigned(Angle);
    }
}
