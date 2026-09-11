using Microsoft.Data.Sqlite;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Tests.Storage;

/// <summary>Bulk-inserts synthetic <c>SensorSamples</c> rows straight via SQL.</summary>
internal static class SampleSeeding
{
    public static void Insert(
        SqliteConnectionFactory factory,
        long startMs,
        int count,
        long stepMs,
        double wallW,
        double kwhDelta,
        double dtSeconds,
        bool gap)
    {
        using var conn = factory.CreateWriteConnection();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            @"INSERT INTO SensorSamples
                (ts_utc_ms, wall_w, cpu_w, gpu_w, dc_w, kwh_delta, dt_seconds, is_gap, quality,
                 cpu_temp, gpu_temp, cpu_load, gpu_load, ram_load)
              VALUES (@ts, @w, NULL, NULL, NULL, @k, @dt, @g, 0, NULL, NULL, NULL, NULL, NULL)";

        var ts = cmd.Parameters.Add("@ts", SqliteType.Integer);
        var w = cmd.Parameters.Add("@w", SqliteType.Real);
        var k = cmd.Parameters.Add("@k", SqliteType.Real);
        var dt = cmd.Parameters.Add("@dt", SqliteType.Real);
        var g = cmd.Parameters.Add("@g", SqliteType.Integer);

        for (var i = 0; i < count; i++)
        {
            ts.Value = startMs + (long)i * stepMs;
            w.Value = wallW;
            k.Value = kwhDelta;
            dt.Value = dtSeconds;
            g.Value = gap ? 1 : 0;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
