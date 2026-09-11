using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Buffers <see cref="SampleRecord"/>s and flushes them in one transaction when the
/// queue hits <see cref="FlushThreshold"/>, every <see cref="FlushIntervalSeconds"/>s,
/// or on dispose. Holds a single long-lived write connection and reuses one prepared
/// INSERT. Each flush also folds the batch's kWh into <c>Meta['total_kwh_lifetime']</c>.
/// </summary>
public sealed class BatchSampleWriter : ISampleWriter
{
    public const int FlushThreshold = 20;
    public const int MaxBatch = 200;
    public const int MaxQueue = 5000;
    public const int FlushIntervalSeconds = 30;

    private const string InsertSql =
        "INSERT INTO SensorSamples " +
        "(ts_utc_ms, wall_w, cpu_w, gpu_w, dc_w, kwh_delta, dt_seconds, is_gap, quality, " +
        " cpu_temp, gpu_temp, cpu_load, gpu_load, ram_load) " +
        "VALUES (@ts,@wall,@cpu,@gpu,@dc,@kwh,@dt,@gap,@q,@ctemp,@gtemp,@cload,@gload,@rload)";

    private const string BumpKwhSql =
        "INSERT INTO Meta(key, value) VALUES('total_kwh_lifetime', @v) " +
        "ON CONFLICT(key) DO UPDATE SET value = CAST(CAST(Meta.value AS REAL) + @v AS TEXT)";

    private readonly ConcurrentQueue<SampleRecord> _queue = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _insert;
    private readonly ILogger<BatchSampleWriter>? _log;
    private readonly Timer _timer;
    private volatile bool _disposed;
    private long _lastDropWarnTicks;

    public BatchSampleWriter(SqliteConnectionFactory factory, ILogger<BatchSampleWriter>? log = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _log = log;
        _connection = factory.CreateWriteConnection();
        _insert = _connection.CreateCommand();
        _insert.CommandText = InsertSql;
        foreach (var name in new[]
                 { "@ts", "@wall", "@cpu", "@gpu", "@dc", "@kwh", "@dt", "@gap", "@q",
                   "@ctemp", "@gtemp", "@cload", "@gload", "@rload" })
            _insert.Parameters.Add(new SqliteParameter(name, null));
        _insert.Prepare();

        var period = TimeSpan.FromSeconds(FlushIntervalSeconds);
        _timer = new Timer(_ => _ = FlushSafeAsync(), null, period, period);
    }

    public void Enqueue(SampleRecord record)
    {
        if (_disposed) return;
        _queue.Enqueue(record);
        TrimQueue();
        if (_queue.Count >= FlushThreshold)
            _ = FlushSafeAsync();
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
        => FlushCoreAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _timer.DisposeAsync().ConfigureAwait(false);
        try { await FlushCoreAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _log?.LogError(ex, "Final sample flush failed on dispose."); }
        _insert.Dispose();
        _connection.Dispose();
        _flushLock.Dispose();
    }

    private async Task FlushSafeAsync()
    {
        try { await FlushCoreAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _log?.LogError(ex, "Background sample flush failed."); }
    }

    private async Task FlushCoreAsync(CancellationToken ct)
    {
        await _flushLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            while (!_queue.IsEmpty)
            {
                var batch = new List<SampleRecord>(MaxBatch);
                while (batch.Count < MaxBatch && _queue.TryDequeue(out var r)) batch.Add(r);
                if (batch.Count == 0) break;

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception ex)
                {
                    _log?.LogError(ex, "Flushing {Count} samples failed; re-queueing.", batch.Count);
                    foreach (var r in batch) _queue.Enqueue(r);
                    TrimQueue();
                    break; // retry on the next tick / enqueue
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private void WriteBatch(List<SampleRecord> batch)
    {
        using var tx = _connection.BeginTransaction();
        _insert.Transaction = tx;

        var kwh = 0d;
        foreach (var r in batch)
        {
            Bind(r);
            _insert.ExecuteNonQuery();
            kwh += r.KwhDelta;
        }

        using (var bump = _connection.CreateCommand())
        {
            bump.Transaction = tx;
            bump.CommandText = BumpKwhSql;
            bump.Parameters.AddWithValue("@v", kwh);
            bump.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private void Bind(SampleRecord r)
    {
        var p = _insert.Parameters;
        p["@ts"].Value = r.TsUtcMs;
        p["@wall"].Value = r.WallW;
        p["@cpu"].Value = (object?)r.CpuW ?? DBNull.Value;
        p["@gpu"].Value = (object?)r.GpuW ?? DBNull.Value;
        p["@dc"].Value = (object?)r.DcW ?? DBNull.Value;
        p["@kwh"].Value = r.KwhDelta;
        p["@dt"].Value = r.DtSeconds;
        p["@gap"].Value = r.IsGap ? 1 : 0;
        p["@q"].Value = r.Quality;
        p["@ctemp"].Value = (object?)r.CpuTemp ?? DBNull.Value;
        p["@gtemp"].Value = (object?)r.GpuTemp ?? DBNull.Value;
        p["@cload"].Value = (object?)r.CpuLoad ?? DBNull.Value;
        p["@gload"].Value = (object?)r.GpuLoad ?? DBNull.Value;
        p["@rload"].Value = (object?)r.RamLoad ?? DBNull.Value;
    }

    private void TrimQueue()
    {
        var dropped = 0;
        while (_queue.Count > MaxQueue && _queue.TryDequeue(out _)) dropped++;
        if (dropped == 0) return;

        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastDropWarnTicks);
        if (now - last <= TimeSpan.FromSeconds(30).Ticks) return;
        Interlocked.Exchange(ref _lastDropWarnTicks, now);
        _log?.LogWarning(
            "Sample queue exceeded {Max}; dropped {Dropped} oldest records (DB write stalled?).",
            MaxQueue, dropped);
    }
}
