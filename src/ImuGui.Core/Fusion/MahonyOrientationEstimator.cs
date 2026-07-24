using ImuGui.Core.Models;

namespace ImuGui.Core.Fusion;

/// <summary>
/// Quaternion-based Mahony MARG filter (gimbal-lock free; the default estimator).
/// Gyroscope rates are integrated on the attitude quaternion; the accelerometer's gravity
/// direction and the magnetometer's field direction provide a proportional-integral error
/// correction. The first update initializes attitude directly from accelerometer + magnetometer.
/// </summary>
public sealed class MahonyOrientationEstimator : IOrientationEstimator
{
    private const double MaxDeltaSeconds = 0.5;
    private const double VectorEpsilonSquared = 1e-12;

    /// <summary>
    /// Anti-windup: the integral correction is clamped to this angular rate. Generous for
    /// realistic gyro biases (≈ 5.7 °/s) while preventing windup during large transients.
    /// </summary>
    private const double MaxIntegralCorrectionRadiansPerSecond = 0.1;

    private readonly MahonyOptions _options;
    private Quaternion _attitude = Quaternion.Identity;
    private Vector3 _integralError = Vector3.Zero;
    private bool _initialized;

    /// <summary>Creates the estimator.</summary>
    /// <param name="options">Gains; defaults when null.</param>
    public MahonyOrientationEstimator(MahonyOptions? options = null)
    {
        _options = options ?? new MahonyOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public Orientation CurrentOrientation => _attitude.ToOrientation();

    /// <inheritdoc />
    public Quaternion CurrentAttitude => _attitude;

    /// <inheritdoc />
    public void Update(SensorSample sample, TimeSpan deltaTime)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!_initialized)
        {
            _attitude = InitialAttitudeFrom(sample);
            _initialized = true;
            return;
        }

        // Clamp pathological gaps (dropped device, paused replay) instead of integrating them.
        double dt = Math.Min(deltaTime.TotalSeconds, MaxDeltaSeconds);
        if (dt <= 0)
        {
            return;
        }

        Vector3 error = Vector3.Zero;

        if (sample.Accelerometer.MagnitudeSquared > VectorEpsilonSquared)
        {
            Vector3 measuredGravityDirection = sample.Accelerometer.Normalized();

            // Predicted specific-force direction: R(q)ᵀ · (0, 0, -1).
            var predicted = new Vector3(
                2 * ((_attitude.W * _attitude.Y) - (_attitude.X * _attitude.Z)),
                -2 * ((_attitude.Y * _attitude.Z) + (_attitude.W * _attitude.X)),
                (2 * ((_attitude.X * _attitude.X) + (_attitude.Y * _attitude.Y))) - 1);

            error += measuredGravityDirection.Cross(predicted);
        }

        if (sample.Magnetometer.MagnitudeSquared > VectorEpsilonSquared)
        {
            Vector3 measuredField = sample.Magnetometer.Normalized();

            // Build the world-frame reference from the current estimate (horizontal
            // component points north), then predict its body-frame direction.
            Vector3 estimatedWorldField = _attitude.Rotate(measuredField);
            double horizontalMagnitude = Math.Sqrt(
                (estimatedWorldField.X * estimatedWorldField.X)
                + (estimatedWorldField.Y * estimatedWorldField.Y));
            var reference = new Vector3(horizontalMagnitude, 0, estimatedWorldField.Z);
            Vector3 predictedField = _attitude.InverseRotate(reference);

            error += measuredField.Cross(predictedField);
        }

        Vector3 correctedRate = DegreesToRadians(sample.Gyroscope);
        if (_options.IntegralGain > 0)
        {
            _integralError += error * dt;
            double integralCorrection = _options.IntegralGain * _integralError.Magnitude;
            if (integralCorrection > MaxIntegralCorrectionRadiansPerSecond)
            {
                _integralError *= MaxIntegralCorrectionRadiansPerSecond / integralCorrection;
            }

            correctedRate += (_options.ProportionalGain * error) + (_options.IntegralGain * _integralError);
        }
        else
        {
            correctedRate += _options.ProportionalGain * error;
        }

        double angle = correctedRate.Magnitude * dt;
        if (angle > 0)
        {
            _attitude = (_attitude * Quaternion.FromAxisAngle(correctedRate, angle)).Normalized();
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _attitude = Quaternion.Identity;
        _integralError = Vector3.Zero;
        _initialized = false;
    }

    private static Quaternion InitialAttitudeFrom(SensorSample sample)
    {
        double roll = 0;
        double pitch = 0;
        if (sample.Accelerometer.MagnitudeSquared > VectorEpsilonSquared)
        {
            roll = TiltCompensation.ComputeRollRadians(sample.Accelerometer);
            pitch = TiltCompensation.ComputePitchRadians(sample.Accelerometer);
        }

        double yaw = sample.Magnetometer.MagnitudeSquared > VectorEpsilonSquared
            ? TiltCompensation.ComputeHeadingRadians(sample.Magnetometer, roll, pitch)
            : 0;

        return Quaternion.FromEulerAngles(roll, pitch, yaw);
    }

    private static Vector3 DegreesToRadians(Vector3 degreesPerSecond) => new(
        AngleMath.DegreesToRadians(degreesPerSecond.X),
        AngleMath.DegreesToRadians(degreesPerSecond.Y),
        AngleMath.DegreesToRadians(degreesPerSecond.Z));
}
