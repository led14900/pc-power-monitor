using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>Maps the <see cref="HardwareType.Memory"/> hardware to a <see cref="MemorySnapshot"/>.</summary>
internal static class MemorySnapshotMapper
{
    public static MemorySnapshot Map(IHardware hw)
    {
        var used = hw.FirstValue(SensorType.Data, "Memory Used");
        var available = hw.FirstValue(SensorType.Data, "Memory Available");
        var load = hw.FirstValue(SensorType.Load, "Memory");

        return new MemorySnapshot(used, available, load);
    }
}
