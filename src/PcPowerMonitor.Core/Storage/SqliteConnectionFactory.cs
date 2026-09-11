using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Infrastructure;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Opens configured SQLite connections. One long-lived connection is used for writes
/// (held by <see cref="BatchSampleWriter"/>); readers open their own — WAL allows
/// concurrent readers. A corrupt file on open is quarantined and a fresh DB created
/// rather than crashing the process.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly ILogger<SqliteConnectionFactory>? _log;

    /// <param name="databasePath">
    /// Override the on-disk location. Production leaves this null → <see cref="AppPaths.DatabaseFile"/>.
    /// Tests pass a throwaway temp path so no global state is touched.
    /// </param>
    public SqliteConnectionFactory(string? databasePath = null, ILogger<SqliteConnectionFactory>? log = null)
    {
        _log = log;
        DatabasePath = databasePath ?? AppPaths.DatabaseFile;
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
    }

    public string DatabasePath { get; }

    /// <summary>Long-lived write connection. Caller keeps it open for the app lifetime.</summary>
    public SqliteConnection CreateWriteConnection() => OpenResilient();

    /// <summary>Fresh read connection per call; dispose with <c>using</c>.</summary>
    public SqliteConnection CreateReadConnection() => OpenResilient();

    private SqliteConnection OpenResilient()
    {
        try
        {
            return Open(DatabasePath);
        }
        catch (SqliteException ex) when (IsCorruption(ex))
        {
            QuarantineCorruptFile(ex);
            return Open(DatabasePath); // fresh file — succeeds or throws a genuine error
        }
    }

    private static SqliteConnection Open(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = true,
        }.ToString();

        var conn = new SqliteConnection(connectionString);
        conn.Open();

        // journal_mode=WAL is persisted in the file (set once is enough); synchronous
        // and busy_timeout are per-connection so they are re-applied every open.
        using var pragma = conn.CreateCommand();
        pragma.CommandText =
            "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private static bool IsCorruption(SqliteException ex)
        => ex.SqliteErrorCode is 11 /* SQLITE_CORRUPT */ or 26 /* SQLITE_NOTADB */;

    private void QuarantineCorruptFile(SqliteException ex)
    {
        SqliteConnection.ClearAllPools();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var dir = Path.GetDirectoryName(DatabasePath)!;
        var dest = Path.Combine(dir, $"monitor.corrupt-{stamp}.db");
        try
        {
            if (File.Exists(DatabasePath)) File.Move(DatabasePath, dest);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var sidecar = DatabasePath + suffix;
                if (File.Exists(sidecar)) File.Delete(sidecar);
            }
            _log?.LogError(ex,
                "Database corrupt; moved to {Dest}. A fresh database will be created. " +
                "The old file is kept for manual recovery.", dest);
        }
        catch (Exception moveEx)
        {
            _log?.LogError(moveEx, "Failed to quarantine corrupt database at {Path}.", DatabasePath);
        }
    }
}
