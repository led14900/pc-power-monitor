using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Billing.Reporting;
using PcPowerMonitor.Core.Storage;
using CostBreakdown = PcPowerMonitor.Core.Storage.CostBreakdown;

namespace PcPowerMonitor.Tests.Storage;

public sealed class RollupServiceTests
{
    // Asia/Saigon is a whole-hour zone. Passing it explicitly makes the timezone
    // assertions deterministic regardless of the host machine's clock.
    private static readonly TimeSpan Saigon = TimeSpan.FromHours(7);

    /// <summary>These tests only assert kWh/uptime, so cost is stubbed to zero.</summary>
    private sealed class ZeroCostCalculator : ICostCalculator
    {
        public CostBreakdown Calculate(double kwh, DateOnly localDate) => new(0d, 0d, 0d);
    }

    /// <summary><see cref="ITariffProvider"/> whose <see cref="ITariffProvider.Current"/> can change between runs.</summary>
    private sealed class MutableTariffProvider : ITariffProvider
    {
        public MutableTariffProvider(TariffSettings initial) => Current = initial;

        public TariffSettings Current { get; set; }
    }

    private static TariffSettings FlatTariff(double unitPriceVnd) =>
        new() { UnitPriceVnd = unitPriceVnd, IncludeVat = false };

    private static RollupService NewService(TempDatabaseFixture fx) =>
        new(fx.Factory, new ZeroCostCalculator(), log: null, localOffset: Saigon);

    [Fact]
    public async Task Hourly_rollup_sums_kwh_delta_not_avg_times_time()
    {
        using var fx = new TempDatabaseFixture();
        // 1 hour of samples at 2s, 200 W, kwh_delta = 0.2 / 1800 each.
        var start = new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        SampleSeeding.Insert(fx.Factory, start, 1800, 2000, 200d, 0.2d / 1800d, 2d, gap: false);

        await NewService(fx).RunAsync();

        using var conn = fx.OpenRead();
        var row = conn.Query(
            "SELECT kwh, avg_w, uptime_seconds, sample_count FROM HourlyRollup",
            r => (Kwh: r.GetDouble(0), AvgW: r.GetDouble(1), Uptime: r.GetDouble(2), Count: r.GetInt64(3)));
        var only = Assert.Single(row);
        Assert.Equal(0.2d, only.Kwh, 4);
        Assert.Equal(200d, only.AvgW, 3);
        Assert.Equal(3600d, only.Uptime, 1);
        Assert.Equal(1800L, only.Count);
    }

    [Fact]
    public async Task Running_rollup_twice_does_not_double_count()
    {
        using var fx = new TempDatabaseFixture();
        var start = new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        SampleSeeding.Insert(fx.Factory, start, 1800, 2000, 200d, 0.2d / 1800d, 2d, gap: false);
        var svc = NewService(fx);

        await svc.RunAsync();
        await svc.RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(0.2d, conn.Query("SELECT kwh FROM HourlyRollup", r => r.GetDouble(0)).Single(), 4);
        Assert.Equal(0.2d, conn.Query("SELECT kwh FROM DailyRollup", r => r.GetDouble(0)).Single(), 4);
        Assert.Equal(0.2d, conn.Query("SELECT kwh FROM MonthlyRollup", r => r.GetDouble(0)).Single(), 4);
    }

    [Fact]
    public async Task Gap_samples_are_excluded_from_kwh_and_uptime()
    {
        using var fx = new TempDatabaseFixture();
        var start = new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        SampleSeeding.Insert(fx.Factory, start, 1800, 2000, 200d, 0.2d / 1800d, 2d, gap: false);
        // 100 gap samples interleaved in the same hour: no energy, no uptime.
        SampleSeeding.Insert(fx.Factory, start + 1, 100, 2000, 200d, 0d, 2d, gap: true);

        await NewService(fx).RunAsync();

        using var conn = fx.OpenRead();
        var row = conn.Query(
            "SELECT kwh, uptime_seconds, sample_count FROM HourlyRollup",
            r => (Kwh: r.GetDouble(0), Uptime: r.GetDouble(1), Count: r.GetInt64(2))).Single();
        Assert.Equal(0.2d, row.Kwh, 4);
        Assert.Equal(3600d, row.Uptime, 1);
        Assert.Equal(1900L, row.Count);
    }

    [Fact]
    public async Task Sample_at_1730_utc_on_jan_31_rolls_into_local_february_first()
    {
        using var fx = new TempDatabaseFixture();
        // 17:30 UTC + 7h = 00:30 local on 2026-02-01.
        var ts = new DateTimeOffset(2026, 1, 31, 17, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        SampleSeeding.Insert(fx.Factory, ts, 1, 2000, 200d, 0.0001d, 2d, gap: false);

        await NewService(fx).RunAsync();

        using var conn = fx.OpenRead();
        Assert.Equal("2026-02-01 00",
            conn.Query("SELECT hour_local FROM HourlyRollup", r => r.GetString(0)).Single());
        Assert.Equal("2026-02-01",
            conn.Query("SELECT date_local FROM DailyRollup", r => r.GetString(0)).Single());
        Assert.Equal("2026-02",
            conn.Query("SELECT month_local FROM MonthlyRollup", r => r.GetString(0)).Single());
    }

    [Fact]
    public async Task Rollup_service_on_100k_samples_completes_within_5_seconds()
    {
        using var fx = new TempDatabaseFixture();
        // Seed 100,000 samples: 1 second apart at 200W
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        SampleSeeding.Insert(fx.Factory, start, 100_000, 1000, 200d, 0.2d / 3600d, 1d, gap: false);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await NewService(fx).RunAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Rollup took {sw.ElapsedMilliseconds}ms, expected < 5000ms (performance regression)");
    }

    [Fact]
    public async Task Rollup_cost_is_an_immutable_snapshot_only_recalculate_history_reprices_the_past()
    {
        using var fx = new TempDatabaseFixture();
        var provider = new MutableTariffProvider(FlatTariff(3000d));
        var cost = new ElectricityCostCalculator(provider);
        var svc = new RollupService(fx.Factory, cost, log: null, localOffset: Saigon);

        // Two closed local days, distinct kWh so their costs are distinguishable.
        SeedDay(fx, new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero), 0.2d);
        SeedDay(fx, new DateTimeOffset(2026, 1, 16, 2, 0, 0, TimeSpan.Zero), 0.3d);

        await svc.RunAsync(); // priced at tariff A (3000)
        Assert.Equal(600d, DailyCost(fx, "2026-01-15"), 2);
        Assert.Equal(900d, DailyCost(fx, "2026-01-16"), 2);

        // Tariff changes and a third day arrives; re-run the scheduled rollup.
        provider.Current = FlatTariff(5000d);
        SeedDay(fx, new DateTimeOffset(2026, 1, 17, 2, 0, 0, TimeSpan.Zero), 0.5d);
        using (var seed = fx.Factory.CreateWriteConnection())
            seed.MetaSet("last_rollup_hour", "0"); // force a full raw re-scan

        await svc.RunAsync();

        // Day 3 priced at B; days 1-2 keep their original A snapshot.
        Assert.Equal(2500d, DailyCost(fx, "2026-01-17"), 2);
        Assert.Equal(600d, DailyCost(fx, "2026-01-15"), 2);
        Assert.Equal(900d, DailyCost(fx, "2026-01-16"), 2);
        Assert.Equal(4000d, MonthlyCost(fx, "2026-01"), 2); // 600 + 900 + 2500

        // The explicit user action reprices everything.
        var report = new EnergyReportService(fx.Factory, cost, provider);
        var changed = await report.RecalculateHistoryAsync(FlatTariff(5000d));

        Assert.Equal(3, changed);
        Assert.Equal(1000d, DailyCost(fx, "2026-01-15"), 2);
        Assert.Equal(1500d, DailyCost(fx, "2026-01-16"), 2);
        Assert.Equal(2500d, DailyCost(fx, "2026-01-17"), 2);
        Assert.Equal(5000d, MonthlyCost(fx, "2026-01"), 2);
    }

    /// <summary>One hour of 2s samples at 200 W whose kWh sums to <paramref name="kwh"/>.</summary>
    private static void SeedDay(TempDatabaseFixture fx, DateTimeOffset startUtc, double kwh)
        => SampleSeeding.Insert(
            fx.Factory, startUtc.ToUnixTimeMilliseconds(), 1800, 2000, 200d, kwh / 1800d, 2d, gap: false);

    private static double DailyCost(TempDatabaseFixture fx, string dateLocal)
    {
        using var conn = fx.OpenRead();
        return conn.Query(
            "SELECT cost_vnd FROM DailyRollup WHERE date_local = @d",
            r => r.GetDouble(0), ("@d", dateLocal)).Single();
    }

    private static double MonthlyCost(TempDatabaseFixture fx, string monthLocal)
    {
        using var conn = fx.OpenRead();
        return conn.Query(
            "SELECT cost_vnd FROM MonthlyRollup WHERE month_local = @m",
            r => r.GetDouble(0), ("@m", monthLocal)).Single();
    }
}
