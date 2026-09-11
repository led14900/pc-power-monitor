using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>Maps a GPU <see cref="IHardware"/> (NVIDIA / AMD / Intel) to a <see cref="GpuSnapshot"/>.</summary>
internal static class GpuSnapshotMapper
{
    public static GpuSnapshot Map(IHardware hw)
    {
        var power = SensorNameMatcher.PickValue(
            hw.Readings(SensorType.Power), SensorNameMatcher.GpuPowerNames);

        var temp = SensorNameMatcher.PickValue(
            hw.Readings(SensorType.Temperature), SensorNameMatcher.GpuTempNames);

        var coreLoad = hw.FirstValue(SensorType.Load, "GPU Core");
        var memUsed = hw.FirstValue(SensorType.SmallData, "GPU Memory Used")
                      ?? hw.FirstValue(SensorType.Data, "GPU Memory Used");
        var memTotal = hw.FirstValue(SensorType.SmallData, "GPU Memory Total")
                       ?? hw.FirstValue(SensorType.Data, "GPU Memory Total");
        var coreClock = hw.FirstValue(SensorType.Clock, "GPU Core");

        return new GpuSnapshot(hw.Name, power, temp, coreLoad, memUsed, memTotal, coreClock);
    }
}
