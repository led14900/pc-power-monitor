using PcPowerMonitor.Core.Infrastructure;

namespace PcPowerMonitor.Tests.Infrastructure;

[Collection(AppPathsCollection.Name)]
public sealed class AppPathsTests : IDisposable
{
    public void Dispose() => AppPaths.OverrideRoot(null);

    [Fact]
    public void RootDir_contains_app_name()
    {
        AppPaths.OverrideRoot(null);
        Assert.Contains("PcPowerMonitor", AppPaths.RootDir);
        Assert.EndsWith("monitor.db", AppPaths.DatabaseFile);
        Assert.EndsWith("settings.json", AppPaths.SettingsFile);
    }

    [Fact]
    public void EnsureCreated_is_idempotent()
    {
        var temp = Path.Combine(Path.GetTempPath(), "pcpm-test-" + Guid.NewGuid().ToString("N"));
        AppPaths.OverrideRoot(temp);
        try
        {
            AppPaths.EnsureCreated();
            AppPaths.EnsureCreated(); // second call must not throw

            Assert.True(Directory.Exists(AppPaths.RootDir));
            Assert.True(Directory.Exists(AppPaths.LogsDir));
            Assert.True(Directory.Exists(AppPaths.DataDir));
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
        }
    }
}
