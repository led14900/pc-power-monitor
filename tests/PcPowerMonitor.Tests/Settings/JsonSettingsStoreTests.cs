using PcPowerMonitor.Core.Infrastructure;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Tests.Settings;

[Collection(AppPathsCollection.Name)]
public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _root;

    public JsonSettingsStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcpm-settings-" + Guid.NewGuid().ToString("N"));
        AppPaths.OverrideRoot(_root);
    }

    public void Dispose()
    {
        AppPaths.OverrideRoot(null);
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Missing_file_writes_defaults()
    {
        var store = new JsonSettingsStore();

        Assert.True(File.Exists(AppPaths.SettingsFile));
        Assert.Equal(1, store.Current.SchemaVersion);
        Assert.Equal(2, store.Current.Sampling.IntervalSeconds);
    }

    [Fact]
    public void Save_then_new_store_round_trips()
    {
        var store = new JsonSettingsStore();
        store.Save(store.Current with { Sampling = new SamplingSettings { IntervalSeconds = 7 } });

        var reloaded = new JsonSettingsStore();
        Assert.Equal(7, reloaded.Current.Sampling.IntervalSeconds);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_quarantines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(AppPaths.SettingsFile, "{ not json");

        var store = new JsonSettingsStore();

        Assert.Equal(2, store.Current.Sampling.IntervalSeconds); // defaults
        Assert.NotEmpty(Directory.GetFiles(_root, "settings.bad-*.json"));
    }

    [Fact]
    public void Second_save_leaves_backup_file()
    {
        var store = new JsonSettingsStore();
        store.Save(store.Current with { Storage = new StorageSettings { RetentionDays = 30 } });
        store.Save(store.Current with { Storage = new StorageSettings { RetentionDays = 31 } });

        Assert.True(File.Exists(Path.Combine(_root, "settings.bak")));
    }

    [Fact]
    public void Changed_event_fires_on_save()
    {
        var store = new JsonSettingsStore();
        AppSettings? seen = null;
        store.Changed += s => seen = s;

        store.Save(store.Current with { Storage = new StorageSettings { RetentionDays = 45 } });

        Assert.NotNull(seen);
        Assert.Equal(45, seen!.Storage.RetentionDays);
    }
}
