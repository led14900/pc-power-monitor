using System.Diagnostics;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Infrastructure;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Sampling;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Tests.Fixtures;
using PcPowerMonitor.Tests.Storage;

namespace PcPowerMonitor.Tests.Integration;

/// <summary>
/// Integration tests verifying core components work together with real SQLite database.
/// No mocks of DB or hardware; uses <see cref="NullSensorReader"/> for determinism.
/// </summary>
[Collection(AppPathsCollection.Name)]
public sealed class SamplingLoopIntegrationTests : IDisposable
{
    private readonly TempDatabaseFixture _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Batch_sample_writer_enqueuing_1000_samples_flushes_all_to_db()
    {
        var writer = new BatchSampleWriter(_db.Factory);

        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int i = 0; i < 1000; i++)
        {
            var record = new SampleRecord(
                TsUtcMs: startMs + i * 1000L,
                WallW: 100 + i * 0.1,
                CpuW: null,
                GpuW: null,
                DcW: null,
                KwhDelta: 0.1,
                DtSeconds: 1,
                IsGap: false,
                Quality: 0,
                CpuTemp: null,
                GpuTemp: null,
                CpuLoad: null,
                GpuLoad: null,
                RamLoad: null);
            writer.Enqueue(record);
        }

        // Flush and close
        await writer.FlushAsync(CancellationToken.None);

        // Assert: exactly 1000 rows in DB
        var count = _db.Factory.CreateReadConnection().Query(
            "SELECT COUNT(*) FROM SensorSamples",
            r => r.GetInt32(0)).FirstOrDefault();
        Assert.Equal(1000, count);
    }

    [Fact]
    public void Energy_query_service_restores_total_kwh_lifetime_from_meta()
    {
        // Write a test value to Meta
        using (var conn = _db.Factory.CreateWriteConnection())
        {
            conn.MetaSet("total_kwh_lifetime", "12.3456");
        }

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var totalKwh = query.GetLastTotalKwh();

        Assert.Equal(12.3456, totalKwh, 4);
    }

    [Fact]
    public void Energy_query_service_returns_zero_for_missing_meta_key()
    {
        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var totalKwh = query.GetLastTotalKwh();

        Assert.Equal(0d, totalKwh);
    }

    [Fact]
    public void Power_estimator_with_null_sensor_reader_produces_fallback_wall_w()
    {
        var reader = new NullSensorReader();
        var snapshot = reader.ReadSnapshot();
        var estimator = new PowerEstimator();
        var profile = new HardwareProfile();

        var estimate = estimator.Estimate(snapshot, profile);

        // With all sensors null, should fallback to baseline (~85W)
        Assert.True(estimate.WallW > 0, "Expected fallback power > 0");
        Assert.Contains("Estimated", estimate.Quality.ToString());
    }

    [Fact]
    public void Energy_accumulator_with_fake_clock_accumulates_energy_correctly()
    {
        var clock = new Tests.Power.FakeElapsedClock { ElapsedSeconds = 2 };
        var accumulator = new EnergyAccumulator(clock, logger: null, intervalSeconds: 2);

        // First tick: seed only, no accumulation
        var inc1 = accumulator.Add(100);
        Assert.Equal(0d, inc1.Kwh, 6);

        // Second tick: 100W for 2s = 200 Ws = 0.0000556 kWh
        var inc2 = accumulator.Add(100);
        Assert.True(inc2.Kwh > 0, "Expected energy accumulation on second tick");
        // 100W * 2s = 200Ws / 3600s = 0.0556 Wh = 0.0000556 kWh
        Assert.InRange(inc2.Kwh, 0.00005, 0.00006);
    }

    [Fact]
    public void Database_migrator_creates_schema_idempotently()
    {
        // Run migrator once
        new DatabaseMigrator(_db.Factory).Migrate();

        // Count tables
        using var conn = _db.Factory.CreateReadConnection();
        var tableCount = conn.Query(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table'",
            r => r.GetInt32(0)).FirstOrDefault();

        Assert.True(tableCount > 0, "Expected schema to be created");

        // Run migrator again (should be idempotent)
        new DatabaseMigrator(_db.Factory).Migrate();

        var tableCountAfter = conn.Query(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table'",
            r => r.GetInt32(0)).FirstOrDefault();

        Assert.Equal(tableCount, tableCountAfter);
    }

    [Fact]
    public void Energy_query_service_aggregates_samples_into_power_series()
    {
        var now = DateTimeOffset.UtcNow;
        var startMs = now.ToUnixTimeMilliseconds();

        // Seed 100 samples at 200W
        SampleSeeding.Insert(_db.Factory, startMs, 100, 100, wallW: 200, kwhDelta: 0.001, dtSeconds: 0.1, gap: false);

        var query = new EnergyQueryService(_db.Factory, TimeSpan.Zero);
        var series = query.GetPowerSeries(now, now.AddSeconds(10), bucketSeconds: 1);

        Assert.NotEmpty(series);
        // Average should be close to 200W
        var avgOfAverages = series.Average(p => p.AvgW);
        Assert.InRange(avgOfAverages, 150, 250); // Loose tolerance for timing variation
    }

    [Fact]
    public async Task Sampling_hosted_service_liveness_starts_ticks_stops_cleanly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "pcpm-liveness-" + Guid.NewGuid().ToString("N"));
        AppPaths.OverrideRoot(tempRoot);
        try
        {
            var store = new JsonSettingsStore();
            store.Save(store.Current with { Sampling = new SamplingSettings { IntervalSeconds = 1 } });
            var svc = new SamplingHostedService(
                new NullSensorReader(),
                new PowerEstimator(),
                new EnergyAccumulator(new StopwatchElapsedClock(), logger: null, intervalSeconds: 1),
                new BatchSampleWriter(_db.Factory),
                new SnapshotBroadcaster(),
                new EnergyQueryService(_db.Factory, TimeSpan.Zero),
                new SamplingOptions { IntervalSeconds = 1 },
                store);

            await svc.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(6));
            var sw = Stopwatch.StartNew();
            await svc.StopAsync(CancellationToken.None);
            sw.Stop();
            svc.Dispose();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"StopAsync took {sw.Elapsed.TotalSeconds:F2}s");
            var count = _db.Factory.CreateReadConnection().Query(
                "SELECT COUNT(*) FROM SensorSamples",
                r => r.GetInt32(0)).FirstOrDefault();
            Assert.True(count >= 4, $"Expected >= 4 samples; got {count}");
            var nullCount = _db.Factory.CreateReadConnection().Query(
                "SELECT COUNT(*) FROM SensorSamples WHERE wall_w IS NULL",
                r => r.GetInt32(0)).FirstOrDefault();
            Assert.Equal(0, nullCount);
        }
        finally
        {
            AppPaths.OverrideRoot(null);
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}
