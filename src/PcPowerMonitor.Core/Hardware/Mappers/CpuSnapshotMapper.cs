using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>Maps an <see cref="IHardware"/> of type <see cref="HardwareType.Cpu"/> to a <see cref="CpuSnapshot"/>.</summary>
internal static class CpuSnapshotMapper
{
    public static CpuSnapshot Map(IHardware hw)
    {
        var power = SensorNameMatcher.PickValue(
            hw.Readings(SensorType.Power), SensorNameMatcher.CpuPackagePowerNames);

        var temp = SensorNameMatcher.PickValue(
            hw.Readings(SensorType.Temperature), SensorNameMatcher.CpuPackageTempNames);

        var load = hw.FirstValue(SensorType.Load, "CPU Total");
        var maxClock = hw.MaxValue(SensorType.Clock);

        return new CpuSnapshot(hw.Name, power, temp, load, maxClock);
    }
}
