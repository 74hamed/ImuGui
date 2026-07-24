namespace ImuGui.Core.Models;

/// <summary>
/// One immutable 9-axis IMU sample plus temperature.
/// Conventions: body frame X forward, Y right, Z down (NED). A level, stationary
/// device reads an accelerometer specific force of (0, 0, -1) g.
/// </summary>
/// <param name="Timestamp">Monotonic time since the source started; used to measure per-sample dt.</param>
/// <param name="Gyroscope">Angular rate around body X/Y/Z in degrees per second.</param>
/// <param name="Accelerometer">Specific force along body X/Y/Z in g.</param>
/// <param name="Magnetometer">Magnetic field along body X/Y/Z in device units (typically µT).</param>
/// <param name="TemperatureCelsius">Sensor temperature in degrees Celsius.</param>
public sealed record SensorSample(
    TimeSpan Timestamp,
    Vector3 Gyroscope,
    Vector3 Accelerometer,
    Vector3 Magnetometer,
    double TemperatureCelsius)
{
    /// <summary>Reads the scalar value of one channel.</summary>
    /// <param name="channel">The channel to read.</param>
    /// <exception cref="ArgumentOutOfRangeException">The channel is not a known value.</exception>
    public double GetChannelValue(SensorChannel channel) => channel switch
    {
        SensorChannel.GyroscopeX => Gyroscope.X,
        SensorChannel.GyroscopeY => Gyroscope.Y,
        SensorChannel.GyroscopeZ => Gyroscope.Z,
        SensorChannel.AccelerometerX => Accelerometer.X,
        SensorChannel.AccelerometerY => Accelerometer.Y,
        SensorChannel.AccelerometerZ => Accelerometer.Z,
        SensorChannel.MagnetometerX => Magnetometer.X,
        SensorChannel.MagnetometerY => Magnetometer.Y,
        SensorChannel.MagnetometerZ => Magnetometer.Z,
        SensorChannel.Temperature => TemperatureCelsius,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown sensor channel."),
    };

    /// <summary>Builds a sample from scalar channel values in <see cref="SensorChannel"/> order.</summary>
    /// <param name="timestamp">The sample timestamp.</param>
    /// <param name="values">Exactly <see cref="SensorChannels.Count"/> values in column order.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> does not contain exactly one value per channel.</exception>
    public static SensorSample FromChannelValues(TimeSpan timestamp, IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != SensorChannels.Count)
        {
            throw new ArgumentException(
                $"Expected {SensorChannels.Count} channel values, got {values.Count}.", nameof(values));
        }

        return new SensorSample(
            timestamp,
            new Vector3(values[0], values[1], values[2]),
            new Vector3(values[3], values[4], values[5]),
            new Vector3(values[6], values[7], values[8]),
            values[9]);
    }
}
