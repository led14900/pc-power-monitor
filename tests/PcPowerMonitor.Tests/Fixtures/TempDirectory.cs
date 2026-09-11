namespace PcPowerMonitor.Tests.Fixtures;

/// <summary>
/// A throwaway directory under the OS temp path. Recursive delete on dispose is
/// best-effort — SQLite WAL sidecars can linger briefly on Windows.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "pcpm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* best effort — leftover temp files are harmless */ }
    }
}
