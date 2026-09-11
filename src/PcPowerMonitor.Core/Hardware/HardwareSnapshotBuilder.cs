using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Hardware.Mappers;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Walks an already-updated <see cref="Computer"/> tree and dispatches each
/// hardware node to the matching group mapper, then assembles the immutable
/// <see cref="HardwareSnapshot"/>. Pure with respect to LHM state — it only reads.
/// </summary>
internal static class HardwareSnapshotBuilder
{
    public static HardwareSnapshot Build(Computer computer, SensorAvailability availability, string? reason)
    {
        CpuSnapshot? cpu = null;
        MemorySnapshot? memory = null;
        double? boardTemp = null;
        var gpus = new List<GpuSnapshot>();
        var disks = new List<DiskSnapshot>();
        var fans = new List<FanSnapshot>();
        var networks = new List<NetworkSnapshot>();

        foreach (var hw in EnumerateAll(computer))
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    cpu ??= CpuSnapshotMapper.Map(hw);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    gpus.Add(GpuSnapshotMapper.Map(hw));
                    break;
                case HardwareType.Memory:
                    memory ??= MemorySnapshotMapper.Map(hw);
                    break;
                case HardwareType.Storage:
                    disks.Add(StorageSnapshotMapper.Map(hw));
                    break;
                case HardwareType.Motherboard:
                case HardwareType.SuperIO:
                case HardwareType.EmbeddedController:
                    fans.AddRange(FanAndBoardSnapshotMapper.MapFans(hw));
                    boardTemp ??= FanAndBoardSnapshotMapper.MapBoardTemp(hw);
                    break;
                case HardwareType.Cooler:
                    fans.AddRange(FanAndBoardSnapshotMapper.MapFans(hw));
                    break;
                case HardwareType.Network:
                    networks.Add(NetworkSnapshotMapper.Map(hw));
                    break;
            }
        }

        return new HardwareSnapshot(
            DateTimeOffset.UtcNow, availability, reason,
            cpu, gpus, memory, disks, fans, networks, boardTemp);
    }

    /// <summary>Depth-first flatten of <c>computer.Hardware</c> including all sub-hardware.</summary>
    public static IEnumerable<IHardware> EnumerateAll(Computer computer)
    {
        foreach (var root in computer.Hardware)
        {
            foreach (var node in Flatten(root))
                yield return node;
        }
    }

    private static IEnumerable<IHardware> Flatten(IHardware hw)
    {
        yield return hw;
        foreach (var sub in hw.SubHardware)
        {
            foreach (var node in Flatten(sub))
                yield return node;
        }
    }
}
