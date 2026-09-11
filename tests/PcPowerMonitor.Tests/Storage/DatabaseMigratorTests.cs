using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Tests.Fixtures;

namespace PcPowerMonitor.Tests.Storage;

public sealed class DatabaseMigratorTests
{
    private static readonly string[] ExpectedTables =
        { "SensorSamples", "HourlyRollup", "DailyRollup", "MonthlyRollup", "Meta" };

    [Fact]
    public void Migrate_creates_full_schema_and_sets_user_version()
    {
        using var dir = new TempDirectory();
        var factory = new SqliteConnectionFactory(Path.Combine(dir.Path, "monitor.db"));

        new DatabaseMigrator(factory).Migrate();

        using var conn = factory.CreateReadConnection();
        var tables = conn.Query(
            "SELECT name FROM sqlite_master WHERE type = 'table'", r => r.GetString(0));
        foreach (var table in ExpectedTables)
            Assert.Contains(table, tables);

        var indexes = conn.Query(
            "SELECT name FROM sqlite_master WHERE type = 'index' AND name = @n",
            r => r.GetString(0), ("@n", "idx_samples_ts"));
        Assert.Single(indexes);

        Assert.Equal(1L, conn.ScalarLong("PRAGMA user_version"));
    }

    [Fact]
    public void Migrate_is_idempotent_when_run_twice()
    {
        using var dir = new TempDirectory();
        var factory = new SqliteConnectionFactory(Path.Combine(dir.Path, "monitor.db"));
        var migrator = new DatabaseMigrator(factory);

        migrator.Migrate();
        migrator.Migrate(); // must not throw and must not duplicate objects

        using var conn = factory.CreateReadConnection();
        Assert.Equal(1L, conn.ScalarLong("PRAGMA user_version"));
        Assert.Equal(
            5L,
            conn.ScalarLong(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"));
    }
}
