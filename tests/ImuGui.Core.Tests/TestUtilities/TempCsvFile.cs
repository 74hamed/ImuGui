namespace ImuGui.Core.Tests.TestUtilities;

/// <summary>A throwaway CSV file on disk, deleted on dispose.</summary>
internal sealed class TempCsvFile : IDisposable
{
    internal TempCsvFile(params string[] lines)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"imugui-test-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(Path, lines);
    }

    internal string Path { get; }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }
}
