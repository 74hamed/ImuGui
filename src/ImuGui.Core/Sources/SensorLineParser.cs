using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ImuGui.Core.Models;

namespace ImuGui.Core.Sources;

/// <summary>
/// Parses one line of the ImuGui sensor text protocol: exactly ten comma-separated numeric
/// fields, invariant culture ('.' decimal separator), in the order
/// <c>GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature</c>.
/// Used verbatim for serial lines; the CSV loader applies the same column contract.
/// </summary>
public static class SensorLineParser
{
    /// <summary>The expected column order, which doubles as the canonical CSV header.</summary>
    public const string ExpectedHeader = "GyroX,GyroY,GyroZ,AccelX,AccelY,AccelZ,MagX,MagY,MagZ,Temperature";

    private const int MaxQuotedFieldLength = 24;

    /// <summary>
    /// Attempts to parse a protocol line into a sample with <see cref="SensorSample.Timestamp"/>
    /// set to zero; the caller stamps arrival time.
    /// </summary>
    /// <param name="line">The raw line, without trailing newline.</param>
    /// <param name="sample">The parsed sample on success.</param>
    /// <param name="error">A human-readable reason on failure.</param>
    public static bool TryParse(
        string? line,
        [NotNullWhen(true)] out SensorSample? sample,
        [NotNullWhen(false)] out string? error)
    {
        sample = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Empty line.";
            return false;
        }

        string[] fields = line.Split(',');
        if (fields.Length != SensorChannels.Count)
        {
            error = $"Expected {SensorChannels.Count} comma-separated values, got {fields.Length}.";
            return false;
        }

        double[] values = new double[SensorChannels.Count];
        for (int i = 0; i < fields.Length; i++)
        {
            string field = fields[i].Trim();
            if (!double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                error = $"Field {i + 1} ({Quote(field)}) is not a valid invariant-culture number.";
                return false;
            }
        }

        sample = SensorSample.FromChannelValues(TimeSpan.Zero, values);
        error = null;
        return true;
    }

    /// <summary>
    /// Heuristically detects a header line: the first field is not parseable as a number
    /// (data rows always begin with a numeric gyro value).
    /// </summary>
    /// <param name="line">The raw line.</param>
    public static bool IsLikelyHeader(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int firstComma = line.IndexOf(',', StringComparison.Ordinal);
        string firstField = (firstComma < 0 ? line : line[..firstComma]).Trim();
        return firstField.Length > 0
            && !double.TryParse(firstField, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static string Quote(string field) =>
        field.Length <= MaxQuotedFieldLength ? $"'{field}'" : $"'{field[..MaxQuotedFieldLength]}…'";
}
