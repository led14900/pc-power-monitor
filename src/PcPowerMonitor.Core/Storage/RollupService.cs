using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Aggregates raw samples into hourly → daily → monthly rollups. Idempotent (upserts,
/// full recompute of daily/monthly each run) and only ever rolls up hours that have
/// fully elapsed. All work happens in one transaction; <c>Meta['last_rollup_hour']</c>
/// bounds the raw scan next time.
/// </summary>
public sealed class RollupService
{
    private const long HourMs = 3_600_000L;
    private const string LastRollupHourKey = "last_rollup_hour";

    private readonly SqliteConnectionFactory _factory;
    private readonly ICostCalculator _cost;
    private readonly ILogger<RollupService>? _log;
    private readonly string _offsetModifiers;

    public RollupService(
        SqliteConnectionFactory factory,
        ICostCalculator cost,
        ILogger<RollupService>? log = null,
        TimeSpan? localOffset = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _cost = cost ?? throw new ArgumentNullException(nameof(cost));
        _log = log;
        _offsetModifiers = RollupSqlStatements.BuildOffsetModifiers(
            localOffset ?? TimeZoneInfo.Local.BaseUtcOffset);
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Rollup failed.");
        }
        return Task.CompletedTask;
    }

    private void Run()
    {
        using var conn = _factory.CreateWriteConnection();

        var minTs = conn.ScalarLong("SELECT MIN(ts_utc_ms) FROM SensorSamples");
        if (minTs is null) return; // no raw data yet

        var lastRollup = ParseLong(conn.MetaGet(LastRollupHourKey)) ?? 0L;
        var from = Math.Max(minTs.Value, lastRollup);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var to = nowMs - (nowMs % HourMs); // start of the current UTC hour → only closed hours
        // NOTE: this scan window aligns to the UTC hour, while buckets group by the LOCAL
        // hour. The two boundaries coincide only for whole-hour local offsets; v1 targets
        // UTC+7 (Vietnam), where rollups are therefore exact. In a 30/45-min-offset zone a
        // local-hour bucket straddles two scan windows and the "ON CONFLICT ... DO UPDATE
        // SET kwh = excluded.kwh" replace would drop the earlier partial hour. Revisit by
        // aligning this bound to the local-hour start before shipping to such zones.
        if (from >= to) return;

        using var tx = conn.BeginTransaction();
        conn.ExecNonQuery(tx, RollupSqlStatements.Hourly(_offsetModifiers), ("@from", from), ("@to", to));
        conn.ExecNonQuery(tx, RollupSqlStatements.Daily);
        ApplyDailyCost(conn, tx);
        conn.ExecNonQuery(tx, RollupSqlStatements.Monthly);
        conn.MetaSet(LastRollupHourKey, to.ToString(CultureInfo.InvariantCulture), tx);
        tx.Commit();

        _log?.LogDebug("Rollup complete through UTC ms {To}.", to);
    }

    private void ApplyDailyCost(SqliteConnection conn, SqliteTransaction tx)
    {
        var days = new List<(string Date, double Kwh)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = RollupSqlStatements.DailyCostSelect;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                days.Add((reader.GetString(0), reader.GetDouble(1)));
        }

        foreach (var (date, kwh) in days)
        {
            var localDate = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var cost = _cost.Calculate(kwh, localDate);
            conn.ExecNonQuery(tx, RollupSqlStatements.DailyCostUpdate,
                ("@c", cost.CostVnd), ("@p", cost.UnitPriceVnd), ("@v", cost.VatRate), ("@d", date));
        }
    }

    private static long? ParseLong(string? text)
        => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
