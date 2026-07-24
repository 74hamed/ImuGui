# Data Formats

## Conventions

ImuGui uses **NED (North-East-Down) aerospace conventions** end to end:

- **Body frame:** X forward, Y right, Z down.
- **Euler order:** ZYX (yaw → pitch → roll). Positive roll = right side down,
  positive pitch = nose up, yaw = compass heading (0° north, increasing clockwise;
  east = 90°).
- **Accelerometer** reports *specific force* in **g**. A level, stationary device reads
  **(0, 0, −1)**. If your device reads +1 g on Z when flat (common for chips mounted
  Z-up), mount or remap accordingly (negate Z and Y to stay right-handed).
- **Gyroscope** reports body angular rates in **degrees per second**.
- **Magnetometer** units are arbitrary (typically µT); fusion only uses the direction.
- **Temperature** is °C.

## CSV file format (replay mode)

Ten numeric columns per row, in this exact order:

```
GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature
```

- **Header row is optional** and auto-detected (a row whose first field is not a number
  is treated as a header).
- Numbers use **invariant culture**: `.` as the decimal separator, `,` only as the
  column delimiter. Scientific notation (`1.5e-3`) is accepted.
- **Malformed rows are skipped and reported** (count + first row numbers in the log),
  never silently zero-filled. A file with no valid rows refuses to load with an
  explanatory error.
- Replay assigns timestamps on an ideal schedule (row *i* at *i / rate*), so measured
  dt equals the configured replay interval. Looping continues the timeline
  monotonically.

A 40-second, 50 Hz synthetic recording ships in [`samples/imu-sample.csv`](../samples/imu-sample.csv)
(level rest → roll sweep → pitch sweep → full 360° yaw → gentle combined motion, with
realistic noise, gyro bias, and a small magnetometer hard-iron offset — try the
calibration workflow on it).

## Serial line protocol (live mode)

One sample per line, newline-terminated (`\n`, optional `\r` tolerated), ASCII:

```
GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature\n
```

- The **same ten fields** and the same invariant-culture number format as the CSV
  contract. Whitespace around fields is tolerated.
- Default framing **115200 baud, 8N1**; DTR is asserted on open (Arduino-style boards
  often gate transmission on it).
- The first line after opening a port is discarded (it is usually a partial frame).
  A device that echoes the header line on boot is tolerated.
- Malformed lines are counted, logged (throttled), and skipped; the stream keeps
  running.
- On device loss the source raises an error, enters **Reconnecting**, and retries every
  2 s until the device returns or the user disconnects.

Arduino-style emitter sketch (one line per sample):

```cpp
Serial.print(gx, 5); Serial.print(',');
// ... gy, gz, ax, ay, az, mx, my, mz ...
Serial.println(temperature, 2);
```
