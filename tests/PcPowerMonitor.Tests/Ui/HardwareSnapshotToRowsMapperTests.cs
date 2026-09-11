using System.Linq;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Ui;

namespace PcPowerMonitor.Tests.Ui;

public sealed class HardwareSnapshotToRowsMapperTests
{
    private static HardwareSnapshot Snapshot(
        CpuSnapshot? cpu = null,
        IReadOnlyList<GpuSnapshot>? gpus = null,
        MemorySnapshot? mem = null,
        IReadOnlyList<DiskSnapshot>? disks = null,
        IReadOnlyList<FanSnapshot>? fans = null,
        IReadOnlyList<NetworkSnapshot>? nets = null,
        double? board = null) => new(
            DateTimeOffset.UnixEpoch,
            SensorAvailability.Full,
            null,
            cpu,
            gpus ?? Array.Empty<GpuSnapshot>(),
            mem,
            disks ?? Array.Empty<DiskSnapshot>(),
            fans ?? Array.Empty<FanSnapshot>(),
            nets ?? Array.Empty<NetworkSnapshot>(),
            board);

    [Fact]
    public void Empty_snapshot_yields_no_rows()
    {
        var rows = HardwareSnapshotToRowsMapper.Map(Snapshot());
        Assert.Empty(rows);
    }

    [Fact]
    public void Groups_appear_in_stable_declared_order()
    {
        var snap = Snapshot(
            cpu: new CpuSnapshot("Ryzen", 65, 60, 30, 4200),
            gpus: new[] { new GpuSnapshot("RTX", 120, 55, 40, 2048, 8192, 1800) },
            mem: new MemorySnapshot(16, 16, 50),
            disks: new[] { new DiskSnapshot("SSD", 40, 5, 60) },
            fans: new[] { new FanSnapshot("CPU Fan", 900) },
            nets: new[] { new NetworkSnapshot("eth0", 10, 20) },
            board: 35);

        var groups = HardwareSnapshotToRowsMapper.Map(snap)
            .Select(r => r.Group).Distinct().ToArray();

        Assert.Equal(
            new[] { "CPU", "GPU", "RAM", "Ổ đĩa", "Quạt", "Mạng", "Mainboard" },
            groups);
    }

    [Fact]
    public void Null_sensor_values_are_preserved_not_zeroed()
    {
        var snap = Snapshot(cpu: new CpuSnapshot("CPU", PackagePowerW: null,
            PackageTempC: 55, TotalLoadPercent: null, MaxClockMhz: null));

        var rows = HardwareSnapshotToRowsMapper.Map(snap);

        var power = rows.Single(r => r.Name == "Điện năng");
        Assert.Null(power.Value);
        var temp = rows.Single(r => r.Name == "Nhiệt độ" && r.Group == "CPU");
        Assert.Equal(55, temp.Value);
        Assert.DoesNotContain(rows, r => r.Value == 0d);
    }

    [Fact]
    public void Multiple_gpus_get_distinct_stable_labels()
    {
        var snap = Snapshot(gpus: new[]
        {
            new GpuSnapshot("iGPU", 15, 45, 10, 512, 2048, 1200),
            new GpuSnapshot(null, 130, 60, 80, 4096, 8192, 1900),
        });

        var first = HardwareSnapshotToRowsMapper.Map(snap);
        var second = HardwareSnapshotToRowsMapper.Map(snap);

        Assert.Contains(first, r => r.Name == "iGPU · Điện năng");
        Assert.Contains(first, r => r.Name == "GPU 2 · Điện năng");
        Assert.Equal(first, second); // deterministic ordering + content
    }

    [Fact]
    public void Group_is_skipped_when_source_object_is_absent()
    {
        var snap = Snapshot(mem: new MemorySnapshot(8, 8, 50));
        var rows = HardwareSnapshotToRowsMapper.Map(snap);

        Assert.All(rows, r => Assert.Equal("RAM", r.Group));
        Assert.Equal(3, rows.Count);
    }
}
