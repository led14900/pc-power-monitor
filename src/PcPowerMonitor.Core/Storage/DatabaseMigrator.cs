using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Advances the database to <see cref="CurrentVersion"/> using <c>PRAGMA user_version</c>
/// as the schema marker. Called once at startup, before any hosted service runs.
/// </summary>
public sealed class DatabaseMigrator
{
    public const int CurrentVersion = 1;

    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<DatabaseMigrator>? _log;

    public DatabaseMigrator(SqliteConnectionFactory factory, ILogger<DatabaseMigrator>? log = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _log = log;
    }

    /// <summary>
    /// Run every outstanding migration step. Idempotent: a DB already at
    /// <see cref="CurrentVersion"/> is a no-op. Failures are logged, not thrown — the
    /// app still starts; individual services fail safe on a bad schema.
    /// </summary>
    public void Migrate()
    {
        try
        {
            using var conn = _factory.CreateWriteConnection();
            var version = ReadUserVersion(conn);
            while (version < CurrentVersion)
                version = ApplyStep(conn, version);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Database migration failed.");
        }
    }

    private static int ReadUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private int ApplyStep(SqliteConnection conn, int fromVersion)
    {
        switch (fromVersion)
        {
            case 0:
                RunScript(conn, DatabaseSchema.V1, toVersion: 1);
                _log?.LogInformation("Applied database schema v1.");
                return 1;
            default:
                throw new InvalidOperationException(
                    $"No migration path from user_version {fromVersion}.");
        }
    }

    private static void RunScript(SqliteConnection conn, string ddl, int toVersion)
    {
        using var tx = conn.BeginTransaction();

        using (var ddlCmd = conn.CreateCommand())
        {
            ddlCmd.Transaction = tx;
            ddlCmd.CommandText = ddl;
            ddlCmd.ExecuteNonQuery();
        }

        using (var versionCmd = conn.CreateCommand())
        {
            versionCmd.Transaction = tx;
            // user_version does not accept parameters; toVersion is an internal constant.
            versionCmd.CommandText = $"PRAGMA user_version = {toVersion};";
            versionCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
