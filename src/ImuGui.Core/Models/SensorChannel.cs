namespace ImuGui.Core.Models;

/// <summary>
/// Identifies one scalar channel of a 9-axis IMU sample (plus temperature). The
/// enumeration order matches the CSV/serial column order.
/// </summary>
public enum SensorChannel
{
    /// <summary>Gyroscope X axis (deg/s).</summary>
    GyroscopeX = 0,

    /// <summary>Gyroscope Y axis (deg/s).</summary>
    GyroscopeY = 1,

    /// <summary>Gyroscope Z axis (deg/s).</summary>
    GyroscopeZ = 2,

    /// <summary>Accelerometer X axis (g).</summary>
    AccelerometerX = 3,

    /// <summary>Accelerometer Y axis (g).</summary>
    AccelerometerY = 4,

    /// <summary>Accelerometer Z axis (g).</summary>
    AccelerometerZ = 5,

    /// <summary>Magnetometer X axis (device units, typically µT).</summary>
    MagnetometerX = 6,

    /// <summary>Magnetometer Y axis (device units, typically µT).</summary>
    MagnetometerY = 7,

    /// <summary>Magnetometer Z axis (device units, typically µT).</summary>
    MagnetometerZ = 8,

    /// <summary>Sensor temperature (°C).</summary>
    Temperature = 9,
}
