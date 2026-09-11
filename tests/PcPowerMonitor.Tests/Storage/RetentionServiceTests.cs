using System.Globalization;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Tests.Settings;

namespace PcPowerMonitor.Tests.Storage;

public sealed class RetentionServiceTests
{
    private const long DayMs = 86_400_000L;

    private static FakeSettingsStore StoreWith(int retentionDays) =>
        new(new AppSettings { Storage = new StorageSettings { RetentionDays = retentionDays } });

    private static void SeedRollupRows(SqliteConnectionFactory factory, long nowMs)
    {
        using var seed = factory.CreateWriteConnection();
        seed.ExecNonQuery(null,
            @"INSERT INTO HourlyRollup(hour_local, kwh, avg_w, max_w, uptime_seconds, sample_count)
              VALUES ('2025-09-01 10', 1.0, 200, 200, 3600, 1800)");
        seed.ExecNonQuery(null,
            @"INSERT INTO DailyRollup(date_local, kwh, avg_w, max_w, uptime_seconds, cost_vnd, unit_price_vnd, vat_rate)
              VALUES ('2025-09-01', 1.0, 200, 200, 3600, 1234, 3460, 0.1)");
        // last_rollup_hour in the future so the age cutoff (retention days) is what bites.
        seed.MetaSet("last_rollup_hour", nowMs.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Deletes_raw_older_than_retention_but_keeps_recent_and_rollups()
    {
        using var fx = new TempDatabaseFixture();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        SampleSeeding.Insert(fx.Factory, now - 120 * DayMs, 10, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 2 * DayMs, 10, 2000, 200d, 0.0001d, 2d, gap: false);
        SeedRollupRows(fx.Factory, now);

        await new RetentionService(fx.Factory, StoreWith(90)).RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(10L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples"));
        Assert.Equal(0L, conn.ScalarLong(
            "SELECT COUNT(*) FROM SensorSamples WHERE ts_utc_ms < @c", ("@c", now - 90 * DayMs)));
        Assert.Equal(1L, conn.ScalarLong("SELECT COUNT(*) FROM HourlyRollup"));
        Assert.Equal(1L, conn.ScalarLong("SELECT COUNT(*) FROM DailyRollup"));
    }

    [Fact]
    public async Task Never_deletes_past_the_last_rolled_up_hour()
    {
        using var fx = new TempDatabaseFixture();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // All rows are ~200 days old (older than 90d) but rollup has not caught up yet.
        SampleSeeding.Insert(fx.Factory, now - 200 * DayMs, 20, 2000, 200d, 0.0001d, 2d, gap: false);
        using (var seed = fx.Factory.CreateWriteConnection())
            seed.MetaSet("last_rollup_hour", (now - 300 * DayMs).ToString(CultureInfo.InvariantCulture));

        await new RetentionService(fx.Factory, StoreWith(90)).RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(20L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples"));
    }

    [Fact]
    public async Task Scheduled_pass_honours_persisted_retention_days_not_the_90_day_default()
    {
        using var fx = new TempDatabaseFixture();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Rows spanning ~200 days, in four batches of 5.
        SampleSeeding.Insert(fx.Factory, now - 200 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 160 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 100 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 2 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SeedRollupRows(fx.Factory, now);

        // 90d default would keep the 200d/160d batches; a persisted 150 must delete them.
        await new RetentionService(fx.Factory, StoreWith(150)).RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(0L, conn.ScalarLong(
            "SELECT COUNT(*) FROM SensorSamples WHERE ts_utc_ms < @c", ("@c", now - 150 * DayMs)));
        Assert.Equal(10L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples")); // 100d + 2d kept
        Assert.Equal(1L, conn.ScalarLong("SELECT COUNT(*) FROM HourlyRollup"));
        Assert.Equal(1L, conn.ScalarLong("SELECT COUNT(*) FROM DailyRollup"));
        Assert.Equal(1L, conn.ScalarLong(
            "SELECT COUNT(*) FROM DailyRollup WHERE cost_vnd = 1234")); // rollup snapshot untouched
    }

    [Fact]
    public async Task Lower_persisted_retention_deletes_more_aggressively()
    {
        using var fx = new TempDatabaseFixture();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        SampleSeeding.Insert(fx.Factory, now - 200 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 160 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 100 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SampleSeeding.Insert(fx.Factory, now - 2 * DayMs, 5, 2000, 200d, 0.0001d, 2d, gap: false);
        SeedRollupRows(fx.Factory, now);

        await new RetentionService(fx.Factory, StoreWith(30)).RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(5L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples")); // only the 2d batch
        Assert.Equal(0L, conn.ScalarLong(
            "SELECT COUNT(*) FROM SensorSamples WHERE ts_utc_ms < @c", ("@c", now - 30 * DayMs)));
    }
}
