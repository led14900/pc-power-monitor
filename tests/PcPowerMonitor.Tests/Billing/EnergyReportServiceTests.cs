using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Billing.Reporting;
using PcPowerMonitor.Tests.Storage;

namespace PcPowerMonitor.Tests.Billing;

public sealed class EnergyReportServiceTests
{
    private static EnergyReportService NewService(TempDatabaseFixture fx, TariffSettings? tariff = null)
    {
        var provider = new FixedTariffProvider(tariff);
        return new EnergyReportService(fx.Factory, new ElectricityCostCalculator(provider), provider);
    }

    private static void SeedMonth(TempDatabaseFixture fx, string monthLocal, double kwh, double costVnd, double uptimeSeconds)
    {
        using var conn = fx.Factory.CreateWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO MonthlyRollup(month_local, kwh, cost_vnd, uptime_seconds, max_w) " +
            "VALUES(@m, @k, @c, @u, 0)";
        cmd.Parameters.AddWithValue("@m", monthLocal);
        cmd.Parameters.AddWithValue("@k", kwh);
        cmd.Parameters.AddWithValue("@c", costVnd);
        cmd.Parameters.AddWithValue("@u", uptimeSeconds);
        cmd.ExecuteNonQuery();
    }

    private static void SeedDay(TempDatabaseFixture fx, string dateLocal, double kwh)
    {
        using var conn = fx.Factory.CreateWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO DailyRollup(date_local, kwh, avg_w, max_w, uptime_seconds, cost_vnd, unit_price_vnd, vat_rate) " +
            "VALUES(@d, @k, 0, 0, 3600, 0, 0, 0)";
        cmd.Parameters.AddWithValue("@d", dateLocal);
        cmd.Parameters.AddWithValue("@k", kwh);
        cmd.ExecuteNonQuery();
    }

    private static double WithVat(double kwh) => kwh * 3460d * 1.1d;

    [Fact]
    public async Task Monthly_report_returns_12_rows_with_9_empty_and_correct_year_total()
    {
        using var fx = new TempDatabaseFixture();
        SeedMonth(fx, "2026-01", 10d, WithVat(10d), 3600d);
        SeedMonth(fx, "2026-02", 20d, WithVat(20d), 7200d);
        SeedMonth(fx, "2026-03", 30d, WithVat(30d), 10_800d);

        var rows = await NewService(fx).GetMonthlyReportAsync(2026);

        Assert.Equal(12, rows.Count);
        Assert.Equal(9, rows.Count(r => r.Kwh == 0d));
        Assert.Equal(WithVat(60d), rows.Sum(r => r.TotalVnd), 3);
        Assert.Equal(60d, rows.Sum(r => r.Kwh), 6);
    }

    [Fact]
    public async Task Monthly_report_computes_month_over_month_delta()
    {
        using var fx = new TempDatabaseFixture();
        SeedMonth(fx, "2026-01", 10d, WithVat(10d), 3600d);
        SeedMonth(fx, "2026-02", 20d, WithVat(20d), 7200d);
        SeedMonth(fx, "2026-03", 30d, WithVat(30d), 10_800d);

        var rows = await NewService(fx).GetMonthlyReportAsync(2026);

        Assert.Null(rows[0].DeltaKwh);          // January, no December 2025
        Assert.Null(rows[0].DeltaPercent);
        Assert.Equal(10d, rows[1].DeltaKwh!.Value, 6);
        Assert.Equal(100d, rows[1].DeltaPercent!.Value, 6);
        Assert.Equal(50d, rows[2].DeltaPercent!.Value, 6);
        Assert.Equal(-100d, rows[3].DeltaPercent!.Value, 6);   // April 0 vs March 30
        Assert.True(rows[1].DeltaVnd > 0d);
    }

    [Fact]
    public async Task January_delta_uses_previous_year_december()
    {
        using var fx = new TempDatabaseFixture();
        SeedMonth(fx, "2025-12", 40d, WithVat(40d), 3600d);
        SeedMonth(fx, "2026-01", 10d, WithVat(10d), 3600d);

        var rows = await NewService(fx).GetMonthlyReportAsync(2026);

        Assert.Equal(-30d, rows[0].DeltaKwh!.Value, 6);
        Assert.Equal(-75d, rows[0].DeltaPercent!.Value, 6);
    }

    [Fact]
    public async Task Yearly_report_groups_by_calendar_year()
    {
        using var fx = new TempDatabaseFixture();
        SeedMonth(fx, "2026-01", 10d, WithVat(10d), 3600d);
        SeedMonth(fx, "2026-02", 20d, WithVat(20d), 7200d);
        SeedMonth(fx, "2026-03", 30d, WithVat(30d), 10_800d);

        var years = await NewService(fx).GetYearlyReportAsync();

        var row = Assert.Single(years);
        Assert.Equal(2026, row.Year);
        Assert.Equal(60d, row.Kwh, 6);
        Assert.Equal(WithVat(60d), row.TotalVnd, 3);
    }

    [Fact]
    public async Task Available_years_include_data_years_and_current_year_newest_first()
    {
        using var fx = new TempDatabaseFixture();
        SeedMonth(fx, "2024-05", 5d, WithVat(5d), 3600d);
        SeedMonth(fx, "2026-03", 30d, WithVat(30d), 3600d);

        var years = await NewService(fx).GetAvailableYearsAsync();

        Assert.Contains(2024, years);
        Assert.Contains(2026, years);
        Assert.Contains(DateTimeOffset.Now.Year, years);
        Assert.Equal(years.OrderByDescending(y => y).ToList(), years);
    }

    [Fact]
    public async Task Recalculate_history_rewrites_daily_and_monthly_cost_snapshots()
    {
        using var fx = new TempDatabaseFixture();
        SeedDay(fx, "2026-01-05", 10d);
        SeedDay(fx, "2026-01-06", 5d);

        var changed = await NewService(fx).RecalculateHistoryAsync(new TariffSettings());

        Assert.Equal(2, changed);

        using var conn = fx.OpenRead();
        using var day = conn.CreateCommand();
        day.CommandText = "SELECT cost_vnd FROM DailyRollup WHERE date_local = '2026-01-05'";
        Assert.Equal(WithVat(10d), Convert.ToDouble(day.ExecuteScalar()), 3);

        using var month = conn.CreateCommand();
        month.CommandText = "SELECT kwh, cost_vnd FROM MonthlyRollup WHERE month_local = '2026-01'";
        using var reader = month.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(15d, reader.GetDouble(0), 6);
        Assert.Equal(WithVat(15d), reader.GetDouble(1), 3);
    }
}
