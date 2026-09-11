using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Thin ADO helpers so the storage services stay under the 200-line budget and never
/// hand-roll parameter binding. Every helper is parameterised — no string concatenation.
/// </summary>
internal static class SqliteCommandExtensions
{
    public static int ExecNonQuery(
        this SqliteConnection conn,
        SqliteTransaction? tx,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = sql;
        AddParams(cmd, parameters);
        return cmd.ExecuteNonQuery();
    }

    public static List<T> Query<T>(
        this SqliteConnection conn,
        string sql,
        Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParams(cmd, parameters);
        using var reader = cmd.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }

    public static long? ScalarLong(
        this SqliteConnection conn,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParams(cmd, parameters);
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public static string? MetaGet(this SqliteConnection conn, string key, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = "SELECT value FROM Meta WHERE key = @k";
        cmd.Parameters.AddWithValue("@k", key);
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : (string)value;
    }

    public static void MetaSet(this SqliteConnection conn, string key, string value, SqliteTransaction? tx = null)
        => conn.ExecNonQuery(
            tx,
            "INSERT INTO Meta(key, value) VALUES(@k, @v) " +
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value",
            ("@k", key), ("@v", value));

    private static void AddParams(SqliteCommand cmd, (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
