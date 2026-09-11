using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Deletes raw <c>SensorSamples</c> older than the persisted
/// <c>StorageSettings.RetentionDays</c> window (default 90), but never past the last
/// rolled-up hour, so nothing is lost before it is aggregated. The window is read live
/// from <see cref="ISettingsStore"/> on every pass, so the scheduled 24h pass and the
/// manual "Cleanup now" button always agree with the value the user saved.
/// Follows up with a <c>VACUUM</c> at most once every 7 days (it locks the whole DB).
/// </summary>
public sealed class RetentionService
{
    private const long DayMs = 86_400_000L;
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 365; // mirrors SettingsValidator's clamp range.
    private const string LastRollupHourKey = "last_rollup_hour";
    private const string LastVacuumKey = "last_vacuum_utc";
    private static readonly long VacuumIntervalMs = (long)TimeSpan.FromDays(7).TotalMilliseconds;

    private readonly SqliteConnectionFactory _factory;
    private readonly ISettingsStore _settings;
    private readonly ILogger<RetentionService>? _log;

    public RetentionService(
        SqliteConnectionFactory factory,
        ISettingsStore settings,
        ILogger<RetentionService>? log = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Retention pass failed.");
        }
        return Task.CompletedTask;
    }

    private void Run()
    {
        using var conn = _factory.CreateWriteConnection();

        var retentionDays = Math.Clamp(
            _settings.Current.Storage.RetentionDays, MinRetentionDays, MaxRetentionDays);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ageCutoff = nowMs - (long)retentionDays * DayMs;
        var lastRollup = ParseLong(conn.MetaGet(LastRollupHourKey)) ?? nowMs;
        var cutoff = Math.Min(ageCutoff, lastRollup);

        var deleted = conn.ExecNonQuery(null,
            "DELETE FROM SensorSamples WHERE ts_utc_ms < @cutoff", ("@cutoff", cutoff));
        _log?.LogInformation(
            "Retention removed {Rows} raw samples older than {Days} days.", deleted, retentionDays);

        MaybeVacuum(conn, nowMs);
    }

    private void MaybeVacuum(SqliteConnection conn, long nowMs)
    {
        var lastVacuum = ParseLong(conn.MetaGet(LastVacuumKey)) ?? 0L;
        if (nowMs - lastVacuum < VacuumIntervalMs) return;

        conn.ExecNonQuery(null, "VACUUM");
        conn.MetaSet(LastVacuumKey, nowMs.ToString(CultureInfo.InvariantCulture));
        _log?.LogInformation("Database VACUUM complete.");
    }

    private static long? ParseLong(string? text)
        => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
