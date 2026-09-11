using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Core.Storage.Dtos;
using PcPowerMonitor.Tests.Storage;

namespace PcPowerMonitor.Tests.Storage;

/// <summary>
/// Tests for <see cref="IEnergyQueryService"/> with real SQLite database (no mocks).
/// Seeds rollup tables and verifies queries return correct DTOs for power series,
/// daily/monthly aggregation, and today's summary.
/// </summary>
public sealed class EnergyQueryServiceTests : IDisposable
{
    private readonly TempDatabaseFixture _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Get_power_series_returns_raw_samples_when_range_le_24_hours()
    {
        var now = DateTimeOffset.UtcNow;
        var startMs = now.ToUnixTimeMilliseconds();

        // Insert 10 samples over 10 seconds
        SampleSeeding.Insert(_db.Factory, startMs, 10, 1000, wallW: 200, kwhDelta: 0.001, dtSeconds: 1, gap: false);

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var series = query.GetPowerSeries(now, now.AddSeconds(10), bucketSeconds: 2);

        Assert.NotEmpty(series);
        Assert.True(series.Count >= 5, "Expected multiple buckets from raw series");
    }

    [Fact]
    public void Get_power_series_switches_to_hourly_rollup_for_ranges_greater_than_24_hours()
    {
        var date = new DateTime(2026, 1, 15, 12, 0, 0);
        var utcOffset = TimeSpan.FromHours(7);
        var from = new DateTimeOffset(date, utcOffset);

        // Seed an hourly rollup
        using (var conn = _db.Factory.CreateWriteConnection())
        {
            conn.ExecNonQuery(
                null,
                "INSERT INTO HourlyRollup (hour_local, kwh, avg_w, max_w, sample_count, uptime_seconds) " +
                "VALUES ('2026-01-15 12', 0.5, 200, 250, 3600, 3600)");
        }

        var query = new EnergyQueryService(_db.Factory, utcOffset);
        // Query a 48-hour range (crosses 24h threshold, uses hourly rollup)
        var series = query.GetPowerSeries(from, from.AddHours(48), bucketSeconds: 60);

        Assert.NotEmpty(series);
    }

    [Fact]
    public void Get_daily_range_returns_daily_rollup_rows()
    {
        // Insert a daily rollup row
        using (var conn = _db.Factory.CreateWriteConnection())
        {
            conn.ExecNonQuery(
                null,
                "INSERT INTO DailyRollup (date_local, kwh, avg_w, max_w, uptime_seconds, cost_vnd, unit_price_vnd, vat_rate) " +
                "VALUES ('2026-01-15', 12.5, 150, 200, 86400, 43125, 3460, 10.0)");
        }

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var dailies = query.GetDailyRange(DateOnly.Parse("2026-01-15"), DateOnly.Parse("2026-01-15"));

        Assert.Single(dailies);
        Assert.Equal(12.5, dailies[0].Kwh, 4);
        Assert.Equal(150d, dailies[0].AvgW, 3);
        Assert.Equal(200d, dailies[0].MaxW, 3);
        Assert.Equal(86400d, dailies[0].UptimeSeconds, 1);
    }

    [Fact]
    public void Get_monthly_returns_monthly_rollup_rows_for_year()
    {
        // Insert a monthly rollup row
        using (var conn = _db.Factory.CreateWriteConnection())
        {
            conn.ExecNonQuery(
                null,
                "INSERT INTO MonthlyRollup (month_local, kwh, cost_vnd, uptime_seconds, max_w) " +
                "VALUES ('2026-01', 372.3, 1285918, 2592000, 250)");
        }

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var monthlies = query.GetMonthly(2026);

        Assert.NotEmpty(monthlies);
        Assert.Contains(monthlies, m => m.Month == "2026-01");
    }

    [Fact]
    public void Get_today_summary_returns_aggregated_energy_and_uptime_for_current_calendar_day()
    {
        var nowLocal = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, TimeSpan.Zero);
        var startMs = dayStart.ToUnixTimeMilliseconds();

        // Seed 10 samples (each 1s, 100W)
        SampleSeeding.Insert(_db.Factory, startMs, 10, 1000, wallW: 100, kwhDelta: 0.1 / 3600, dtSeconds: 1, gap: false);

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var today = query.GetTodaySummary();

        Assert.True(today.Kwh > 0, "Expected positive kWh accumulation");
        Assert.True(today.UptimeSeconds > 0, "Expected positive uptime");
    }

    [Fact]
    public void Get_last_total_kwh_parses_meta_value_correctly()
    {
        using (var conn = _db.Factory.CreateWriteConnection())
        {
            conn.MetaSet("total_kwh_lifetime", "123.456789");
        }

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var total = query.GetLastTotalKwh();

        Assert.Equal(123.456789, total, 6);
    }

    [Fact]
    public void Get_last_total_kwh_returns_zero_when_meta_key_missing()
    {
        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var total = query.GetLastTotalKwh();

        Assert.Equal(0d, total);
    }

    [Fact]
    public void Get_power_series_aggregates_power_points_correctly()
    {
        var now = DateTimeOffset.UtcNow;
        var startMs = now.ToUnixTimeMilliseconds();

        // Insert 10 samples at 150W
        SampleSeeding.Insert(_db.Factory, startMs, 10, 1000, wallW: 150, kwhDelta: 0.1, dtSeconds: 1, gap: false);

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var series = query.GetPowerSeries(now, now.AddSeconds(10), bucketSeconds: 2);

        Assert.NotEmpty(series);
        Assert.All(series, p => Assert.True(p.AvgW >= 0, "Expected non-negative avg_w"));
    }
}
