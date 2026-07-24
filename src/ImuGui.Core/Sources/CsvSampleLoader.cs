using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ImuGui.Core.Models;

namespace ImuGui.Core.Sources;

/// <summary>Result of loading a sensor CSV file.</summary>
/// <param name="Samples">The valid samples, timestamps zeroed (the replay source stamps them).</param>
/// <param name="MalformedRowCount">How many rows failed to parse and were skipped.</param>
/// <param name="MalformedRowDetails">Row-level details for the first few malformed rows.</param>
/// <param name="HadHeaderRow">Whether a header row was detected and skipped.</param>
internal sealed record CsvLoadResult(
    IReadOnlyList<SensorSample> Samples,
    int MalformedRowCount,
    IReadOnlyList<string> MalformedRowDetails,
    bool HadHeaderRow);

/// <summary>
/// Loads sensor CSV files via CsvHelper with invariant culture and an explicit column map.
/// The header row is optional and auto-detected; malformed rows are skipped and reported,
/// never silently zero-filled.
/// </summary>
internal static class CsvSampleLoader
{
    private const int MaxRecordedDetails = 20;

    internal static async Task<CsvLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken)
    {
        bool hasHeader = await DetectHeaderAsync(filePath, cancellationToken);

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = hasHeader,
            TrimOptions = TrimOptions.Trim,
        };

        var samples = new List<SensorSample>();
        var malformedDetails = new List<string>();
        int malformedCount = 0;

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, configuration);
        csv.Context.RegisterClassMap<SensorCsvRowMap>();

        if (hasHeader)
        {
            if (!await csv.ReadAsync())
            {
                return new CsvLoadResult(samples, 0, malformedDetails, hasHeader);
            }

            csv.ReadHeader();
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SensorCsvRow row = csv.GetRecord<SensorCsvRow>();
                samples.Add(new SensorSample(
                    TimeSpan.Zero,
                    new Vector3(row.GyroX, row.GyroY, row.GyroZ),
                    new Vector3(row.AccelX, row.AccelY, row.AccelZ),
                    new Vector3(row.MagX, row.MagY, row.MagZ),
                    row.Temperature));
            }
            catch (CsvHelperException ex)
            {
                malformedCount++;
                if (malformedDetails.Count < MaxRecordedDetails)
                {
                    malformedDetails.Add($"Row {csv.Parser.RawRow}: {RootMessage(ex)}");
                }
            }
        }

        return new CsvLoadResult(samples, malformedCount, malformedDetails, hasHeader);
    }

    private static async Task<bool> DetectHeaderAsync(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return SensorLineParser.IsLikelyHeader(line);
            }
        }

        return false;
    }

    private static string RootMessage(Exception exception)
    {
        Exception root = exception.GetBaseException();
        string firstLine = root.Message.Split('\n')[0].Trim();
        return firstLine.Length > 0 ? firstLine : root.GetType().Name;
    }

    private sealed class SensorCsvRow
    {
        public double GyroX { get; set; }

        public double GyroY { get; set; }

        public double GyroZ { get; set; }

        public double AccelX { get; set; }

        public double AccelY { get; set; }

        public double AccelZ { get; set; }

        public double MagX { get; set; }

        public double MagY { get; set; }

        public double MagZ { get; set; }

        public double Temperature { get; set; }
    }

    private sealed class SensorCsvRowMap : ClassMap<SensorCsvRow>
    {
        public SensorCsvRowMap()
        {
            Map(m => m.GyroX).Name("GyroX").Index(0);
            Map(m => m.GyroY).Name("GyroY").Index(1);
            Map(m => m.GyroZ).Name("GyroZ").Index(2);
            Map(m => m.AccelX).Name("AccelX").Index(3);
            Map(m => m.AccelY).Name("AccelY").Index(4);
            Map(m => m.AccelZ).Name("AccelZ").Index(5);
            Map(m => m.MagX).Name("MagX").Index(6);
            Map(m => m.MagY).Name("MagY").Index(7);
            Map(m => m.MagZ).Name("MagZ").Index(8);
            Map(m => m.Temperature).Name("Temperature").Index(9);
        }
    }
}
