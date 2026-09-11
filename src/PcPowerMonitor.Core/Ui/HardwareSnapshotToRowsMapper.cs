using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Ui;

/// <summary>
/// Flattens a <see cref="HardwareSnapshot"/> into an ordered, stable list of display
/// rows for the "Phần cứng" tab. Pure and WPF-free so it can be unit tested.
/// A group appears only when its source object exists; individual null fields are
/// kept as null rows (the UI renders them as "—", never "0").
/// </summary>
public static class HardwareSnapshotToRowsMapper
{
    public const string GroupCpu = "CPU";
    public const string GroupGpu = "GPU";
    public const string GroupRam = "RAM";
    public const string GroupDisk = "Ổ đĩa";
    public const string GroupFan = "Quạt";
    public const string GroupNetwork = "Mạng";
    public const string GroupBoard = "Mainboard";

    /// <summary>One display row: which group, sensor label, current value (nullable), unit.</summary>
    public readonly record struct Row(string Group, string Name, double? Value, string Unit);

    public static IReadOnlyList<Row> Map(HardwareSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var rows = new List<Row>(32);

        AddCpu(rows, snapshot.Cpu);
        AddGpus(rows, snapshot.Gpus);
        AddRam(rows, snapshot.Memory);
        AddDisks(rows, snapshot.Disks);
        AddFans(rows, snapshot.Fans);
        AddNetworks(rows, snapshot.Networks);
        AddBoard(rows, snapshot.MainboardTempC);

        return rows;
    }

    private static void AddCpu(List<Row> rows, CpuSnapshot? cpu)
    {
        if (cpu is null) return;
        rows.Add(new(GroupCpu, "Điện năng", cpu.PackagePowerW, "W"));
        rows.Add(new(GroupCpu, "Nhiệt độ", cpu.PackageTempC, "°C"));
        rows.Add(new(GroupCpu, "Tải", cpu.TotalLoadPercent, "%"));
        rows.Add(new(GroupCpu, "Xung nhịp", cpu.MaxClockMhz, "MHz"));
    }

    private static void AddGpus(List<Row> rows, IReadOnlyList<GpuSnapshot>? gpus)
    {
        if (gpus is null) return;
        for (var i = 0; i < gpus.Count; i++)
        {
            var g = gpus[i];
            var label = string.IsNullOrWhiteSpace(g.Name) ? $"GPU {i + 1}" : g.Name!;
            rows.Add(new(GroupGpu, $"{label} · Điện năng", g.PowerW, "W"));
            rows.Add(new(GroupGpu, $"{label} · Nhiệt độ", g.TempC, "°C"));
            rows.Add(new(GroupGpu, $"{label} · Tải", g.CoreLoadPercent, "%"));
            rows.Add(new(GroupGpu, $"{label} · VRAM dùng", g.MemoryUsedMb, "MB"));
            rows.Add(new(GroupGpu, $"{label} · Xung nhân", g.CoreClockMhz, "MHz"));
        }
    }

    private static void AddRam(List<Row> rows, MemorySnapshot? mem)
    {
        if (mem is null) return;
        rows.Add(new(GroupRam, "Đã dùng", mem.UsedGb, "GB"));
        rows.Add(new(GroupRam, "Còn trống", mem.AvailableGb, "GB"));
        rows.Add(new(GroupRam, "Tải", mem.LoadPercent, "%"));
    }

    private static void AddDisks(List<Row> rows, IReadOnlyList<DiskSnapshot>? disks)
    {
        if (disks is null) return;
        for (var i = 0; i < disks.Count; i++)
        {
            var d = disks[i];
            var label = string.IsNullOrWhiteSpace(d.Name) ? $"Ổ đĩa {i + 1}" : d.Name!;
            rows.Add(new(GroupDisk, $"{label} · Nhiệt độ", d.TempC, "°C"));
            rows.Add(new(GroupDisk, $"{label} · Hoạt động", d.ActivityPercent, "%"));
            rows.Add(new(GroupDisk, $"{label} · Đã dùng", d.UsedPercent, "%"));
        }
    }

    private static void AddFans(List<Row> rows, IReadOnlyList<FanSnapshot>? fans)
    {
        if (fans is null) return;
        for (var i = 0; i < fans.Count; i++)
        {
            var f = fans[i];
            var label = string.IsNullOrWhiteSpace(f.Name) ? $"Quạt {i + 1}" : f.Name!;
            rows.Add(new(GroupFan, label, f.Rpm, "RPM"));
        }
    }

    private static void AddNetworks(List<Row> rows, IReadOnlyList<NetworkSnapshot>? nets)
    {
        if (nets is null) return;
        for (var i = 0; i < nets.Count; i++)
        {
            var n = nets[i];
            var label = string.IsNullOrWhiteSpace(n.Name) ? $"Mạng {i + 1}" : n.Name!;
            rows.Add(new(GroupNetwork, $"{label} · Tải lên", n.UploadKBps, "KB/s"));
            rows.Add(new(GroupNetwork, $"{label} · Tải xuống", n.DownloadKBps, "KB/s"));
        }
    }

    private static void AddBoard(List<Row> rows, double? boardTempC)
    {
        if (boardTempC is null) return;
        rows.Add(new(GroupBoard, "Nhiệt độ", boardTempC, "°C"));
    }
}
