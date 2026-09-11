using System.Globalization;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// SQL for the hourly → daily → monthly rollup. Kept apart from
/// <see cref="RollupService"/> because the statements are long.
///
/// kWh is always <c>SUM(kwh_delta)</c> — never <c>avg_w x time</c>, which double-counts
/// sleep gaps. Buckets are keyed by <em>local</em> time (EVN bills by the Vietnamese
/// calendar month): the UTC epoch is shifted by the OS clock's offset, injected as a
/// SQLite datetime modifier. The offset string is derived from
/// <see cref="TimeZoneInfo"/> (or an explicit value in tests) — it is never a hardcoded
/// <c>+7</c>, and never user input, so string-formatting it into the SQL is safe.
/// </summary>
public static class RollupSqlStatements
{
    /// <summary>e.g. <c>'+7 hours'</c> or <c>'+5 hours', '+30 minutes'</c> for a 5:30 zone.</summary>
    public static string BuildOffsetModifiers(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var hours = Math.Abs(offset.Hours);
        var minutes = Math.Abs(offset.Minutes);
        var text = string.Format(CultureInfo.InvariantCulture, "'{0}{1} hours'", sign, hours);
        if (minutes != 0)
            text += string.Format(CultureInfo.InvariantCulture, ", '{0}{1} minutes'", sign, minutes);
        return text;
    }

    /// <summary>Upsert closed hours in <c>[@from, @to)</c> (Unix ms) into HourlyRollup.</summary>
    public static string Hourly(string offsetModifiers) => $@"
INSERT INTO HourlyRollup(hour_local, kwh, avg_w, max_w, uptime_seconds, sample_count)
SELECT strftime('%Y-%m-%d %H', ts_utc_ms / 1000, 'unixepoch', {offsetModifiers}) AS hour_local,
       SUM(kwh_delta),
       AVG(wall_w),
       MAX(wall_w),
       SUM(CASE WHEN is_gap = 0 THEN dt_seconds ELSE 0 END),
       COUNT(*)
FROM SensorSamples
WHERE ts_utc_ms >= @from AND ts_utc_ms < @to
GROUP BY hour_local
ON CONFLICT(hour_local) DO UPDATE SET
  kwh = excluded.kwh, avg_w = excluded.avg_w, max_w = excluded.max_w,
  uptime_seconds = excluded.uptime_seconds, sample_count = excluded.sample_count;";

    /// <summary>
    /// Recompute every DailyRollup row from HourlyRollup. Cost columns are untouched here —
    /// they are a one-time immutable snapshot, filled once by <see cref="DailyCostSelect"/>.
    /// </summary>
    public const string Daily = @"
INSERT INTO DailyRollup(date_local, kwh, avg_w, max_w, uptime_seconds)
SELECT substr(hour_local, 1, 10) AS date_local,
       SUM(kwh),
       SUM(avg_w * sample_count) / NULLIF(SUM(sample_count), 0),
       MAX(max_w),
       SUM(uptime_seconds)
FROM HourlyRollup
GROUP BY date_local
ON CONFLICT(date_local) DO UPDATE SET
  kwh = excluded.kwh, avg_w = excluded.avg_w,
  max_w = excluded.max_w, uptime_seconds = excluded.uptime_seconds;";

    /// <summary>
    /// Days that still need their one-time cost snapshot. Stored cost is immutable: a
    /// later EVN tariff change must NOT silently rewrite history (that is the explicit job
    /// of <c>EnergyReportService.RecalculateHistoryAsync</c>). So we only price rows still
    /// at the schema DEFAULT 0 sentinel (see <see cref="DatabaseSchema"/>:
    /// <c>cost_vnd NOT NULL DEFAULT 0</c>). <c>kwh &gt; 0</c> skips genuine zero-energy days
    /// so they are not re-scanned forever.
    /// </summary>
    public const string DailyCostSelect =
        "SELECT date_local, kwh FROM DailyRollup WHERE cost_vnd = 0 AND kwh > 0;";

    public const string DailyCostUpdate =
        "UPDATE DailyRollup SET cost_vnd = @c, unit_price_vnd = @p, vat_rate = @v WHERE date_local = @d;";

    /// <summary>Recompute every MonthlyRollup row from DailyRollup.</summary>
    public const string Monthly = @"
INSERT INTO MonthlyRollup(month_local, kwh, cost_vnd, uptime_seconds, max_w)
SELECT substr(date_local, 1, 7) AS month_local,
       SUM(kwh), SUM(cost_vnd), SUM(uptime_seconds), MAX(max_w)
FROM DailyRollup
GROUP BY month_local
ON CONFLICT(month_local) DO UPDATE SET
  kwh = excluded.kwh, cost_vnd = excluded.cost_vnd,
  uptime_seconds = excluded.uptime_seconds, max_w = excluded.max_w;";
}
