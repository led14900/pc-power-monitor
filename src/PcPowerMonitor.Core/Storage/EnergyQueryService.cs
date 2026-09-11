using System.Globalization;
using PcPowerMonitor.Core.Storage.Dtos;

namespace PcPowerMonitor.Core.Storage;

/// <inheritdoc />
public sealed class EnergyQueryService : IEnergyQueryService
{
    private const string LocalHourFormat = "yyyy-MM-dd HH";
    private const string LocalDateFormat = "yyyy-MM-dd";
    private static readonly TimeSpan RawSeriesLimit = TimeSpan.FromHours(24);

    private readonly SqliteConnectionFactory _factory;
    private readonly TimeSpan _localOffset;

    public EnergyQueryService(SqliteConnectionFactory factory, TimeSpan? localOffset = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _localOffset = localOffset ?? TimeZoneInfo.Local.BaseUtcOffset;
    }

    public IReadOnlyList<PowerPoint> GetPowerSeries(DateTimeOffset from, DateTimeOffset to, int bucketSeconds)
    {
        if (bucketSeconds <= 0) bucketSeconds = 60;
        return (to - from) <= RawSeriesLimit
            ? RawSeries(from, to, bucketSeconds)
            : HourlySeries(from, to);
    }

    private List<PowerPoint> RawSeries(DateTimeOffset from, DateTimeOffset to, int bucketSeconds)
    {
        using var conn = _factory.CreateReadConnection();
        long bucketMs = bucketSeconds * 1000L;
        return conn.Query(
            @"SELECT (ts_utc_ms / @bucket) * @bucket AS bkt,
                     AVG(wall_w), MAX(wall_w), SUM(kwh_delta)
              FROM SensorSamples
              WHERE ts_utc_ms >= @from AND ts_utc_ms < @to
              GROUP BY bkt ORDER BY bkt",
            r => new PowerPoint(
                DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(0)),
                r.IsDBNull(1) ? 0d : r.GetDouble(1),
                r.IsDBNull(2) ? 0d : r.GetDouble(2),
                r.IsDBNull(3) ? 0d : r.GetDouble(3)),
            ("@bucket", bucketMs),
            ("@from", from.ToUnixTimeMilliseconds()),
            ("@to", to.ToUnixTimeMilliseconds()));
    }

    private List<PowerPoint> HourlySeries(DateTimeOffset from, DateTimeOffset to)
    {
        using var conn = _factory.CreateReadConnection();
        return conn.Query(
            @"SELECT hour_local, kwh, avg_w, max_w FROM HourlyRollup
              WHERE hour_local >= @from AND hour_local < @to ORDER BY hour_local",
            r => new PowerPoint(
                ParseLocalHour(r.GetString(0)),
                r.IsDBNull(2) ? 0d : r.GetDouble(2),
                r.IsDBNull(3) ? 0d : r.GetDouble(3),
                r.IsDBNull(1) ? 0d : r.GetDouble(1)),
            ("@from", ToLocal(from).ToString(LocalHourFormat, CultureInfo.InvariantCulture)),
            ("@to", ToLocal(to).ToString(LocalHourFormat, CultureInfo.InvariantCulture)));
    }

    public IReadOnlyList<DailyEnergy> GetDailyRange(DateOnly from, DateOnly to)
    {
        using var conn = _factory.CreateReadConnection();
        return conn.Query(
            @"SELECT date_local, kwh, avg_w, max_w, uptime_seconds, cost_vnd, unit_price_vnd, vat_rate
              FROM DailyRollup
              WHERE date_local >= @from AND date_local <= @to ORDER BY date_local",
            r => new DailyEnergy(
                DateOnly.ParseExact(r.GetString(0), LocalDateFormat, CultureInfo.InvariantCulture),
                r.GetDouble(1),
                r.IsDBNull(2) ? 0d : r.GetDouble(2),
                r.IsDBNull(3) ? 0d : r.GetDouble(3),
                r.GetDouble(4), r.GetDouble(5), r.GetDouble(6), r.GetDouble(7)),
            ("@from", from.ToString(LocalDateFormat, CultureInfo.InvariantCulture)),
            ("@to", to.ToString(LocalDateFormat, CultureInfo.InvariantCulture)));
    }

    public IReadOnlyList<MonthlyEnergy> GetMonthly(int year)
    {
        using var conn = _factory.CreateReadConnection();
        return conn.Query(
            @"SELECT month_local, kwh, cost_vnd, uptime_seconds, max_w FROM MonthlyRollup
              WHERE month_local >= @from AND month_local <= @to ORDER BY month_local",
            r => new MonthlyEnergy(
                r.GetString(0), r.GetDouble(1), r.GetDouble(2), r.GetDouble(3),
                r.IsDBNull(4) ? 0d : r.GetDouble(4)),
            ("@from", year.ToString("D4", CultureInfo.InvariantCulture) + "-00"),
            ("@to", year.ToString("D4", CultureInfo.InvariantCulture) + "-13"));
    }

    public TodaySummary GetTodaySummary()
    {
        var nowLocal = ToLocal(DateTimeOffset.UtcNow);
        var startLocal = new DateTimeOffset(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, _localOffset);
        var fromMs = startLocal.ToUnixTimeMilliseconds();
        var toMs = startLocal.AddDays(1).ToUnixTimeMilliseconds();

        using var conn = _factory.CreateReadConnection();
        var row = conn.Query(
            @"SELECT COALESCE(SUM(kwh_delta), 0),
                     COALESCE(MAX(wall_w), 0),
                     COALESCE(AVG(wall_w), 0),
                     COALESCE(SUM(CASE WHEN is_gap = 0 THEN dt_seconds ELSE 0 END), 0)
              FROM SensorSamples
              WHERE ts_utc_ms >= @from AND ts_utc_ms < @to",
            r => new TodaySummary(
                DateOnly.FromDateTime(startLocal.DateTime),
                r.GetDouble(0), r.GetDouble(2), r.GetDouble(1), r.GetDouble(3)),
            ("@from", fromMs), ("@to", toMs));
        return row.Count > 0
            ? row[0]
            : new TodaySummary(DateOnly.FromDateTime(startLocal.DateTime), 0d, 0d, 0d, 0d);
    }

    public double GetLastTotalKwh()
    {
        using var conn = _factory.CreateReadConnection();
        var text = conn.MetaGet("total_kwh_lifetime");
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0d;
    }

    private DateTimeOffset ToLocal(DateTimeOffset value) => value.ToOffset(_localOffset);

    private DateTimeOffset ParseLocalHour(string hourLocal)
    {
        var naive = DateTime.ParseExact(hourLocal, LocalHourFormat, CultureInfo.InvariantCulture);
        return new DateTimeOffset(naive, _localOffset);
    }
}
