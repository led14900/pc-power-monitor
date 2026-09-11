using System.Globalization;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Tests.Storage;

public sealed class BatchSampleWriterTests
{
    private static SampleRecord Sample(long ts, double kwhDelta) => new(
        TsUtcMs: ts, WallW: 200d, CpuW: 50d, GpuW: 100d, DcW: 180d,
        KwhDelta: kwhDelta, DtSeconds: 2d, IsGap: false,
        Quality: (int)EstimationQuality.Measured,
        CpuTemp: 60d, GpuTemp: 55d, CpuLoad: 20d, GpuLoad: 30d, RamLoad: 40d);

    [Fact]
    public async Task Flush_persists_every_enqueued_sample_and_accumulates_lifetime_kwh()
    {
        using var fx = new TempDatabaseFixture();
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await using (var writer = new BatchSampleWriter(fx.Factory))
        {
            for (var i = 0; i < 1000; i++)
                writer.Enqueue(Sample(start + i * 2000L, 0.0001d));
            await writer.FlushAsync();

            using (var conn = fx.OpenRead())
            {
                Assert.Equal(1000L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples"));
                Assert.Equal(0.1d, ReadLifetimeKwh(conn), 5);
            }

            for (var i = 0; i < 500; i++)
                writer.Enqueue(Sample(start + (2000 + i) * 2000L, 0.0002d));
            await writer.FlushAsync();
        }

        using var readBack = fx.OpenRead();
        Assert.Equal(1500L, readBack.ScalarLong("SELECT COUNT(*) FROM SensorSamples"));
        // 1000 * 0.0001 + 500 * 0.0002 = 0.2
        Assert.Equal(0.2d, ReadLifetimeKwh(readBack), 5);
    }

    [Fact]
    public async Task DisposeAsync_flushes_remaining_buffer()
    {
        using var fx = new TempDatabaseFixture();
        var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var writer = new BatchSampleWriter(fx.Factory);
        writer.Enqueue(Sample(start, 0.0001d));
        writer.Enqueue(Sample(start + 2000L, 0.0001d));
        await writer.DisposeAsync();

        using var conn = fx.OpenRead();
        Assert.Equal(2L, conn.ScalarLong("SELECT COUNT(*) FROM SensorSamples"));
    }

    private static double ReadLifetimeKwh(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        var text = conn.MetaGet("total_kwh_lifetime");
        Assert.NotNull(text);
        return double.Parse(text!, CultureInfo.InvariantCulture);
    }
}
