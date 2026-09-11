using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>Maps one <see cref="HardwareType.Storage"/> drive to a <see cref="DiskSnapshot"/>.</summary>
internal static class StorageSnapshotMapper
{
    private static readonly string[] TempNames = { "Temperature", "Temperature 1", "Drive Temperature" };

    public static DiskSnapshot Map(IHardware hw)
    {
        var temp = SensorNameMatcher.PickValue(hw.Readings(SensorType.Temperature), TempNames);
        var activity = hw.FirstValue(SensorType.Load, "Total Activity");
        var used = hw.FirstValue(SensorType.Load, "Used Space");

        return new DiskSnapshot(hw.Name, temp, activity, used);
    }
}
