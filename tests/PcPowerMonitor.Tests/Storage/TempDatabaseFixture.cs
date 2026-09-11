using Microsoft.Data.Sqlite;
using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Tests.Fixtures;

namespace PcPowerMonitor.Tests.Storage;

/// <summary>
/// A real, migrated SQLite database in a temp directory. No mocks, no global state
/// (<see cref="SqliteConnectionFactory"/> takes an explicit path), so test classes stay
/// parallel-safe.
/// </summary>
public sealed class TempDatabaseFixture : IDisposable
{
    private readonly TempDirectory _dir = new();

    public TempDatabaseFixture()
    {
        DbPath = Path.Combine(_dir.Path, "monitor.db");
        Factory = new SqliteConnectionFactory(DbPath);
        new DatabaseMigrator(Factory).Migrate();
    }

    public SqliteConnectionFactory Factory { get; }

    public string DbPath { get; }

    public SqliteConnection OpenRead() => Factory.CreateReadConnection();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _dir.Dispose();
    }
}
