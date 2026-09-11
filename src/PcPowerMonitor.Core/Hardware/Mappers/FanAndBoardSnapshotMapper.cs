using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>
/// Extracts fan RPMs and a representative mainboard temperature from motherboard /
/// SuperIO / embedded-controller / cooler hardware.
/// </summary>
internal static class FanAndBoardSnapshotMapper
{
    private static readonly string[] BoardTempNames =
        { "Motherboard", "System", "Mainboard", "Chipset", "VRM", "CPU" };

    public static IEnumerable<FanSnapshot> MapFans(IHardware hw)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == SensorType.Fan)
                yield return new FanSnapshot(s.Name, (double?)s.Value);
        }
    }

    public static double? MapBoardTemp(IHardware hw) =>
        SensorNameMatcher.PickValue(hw.Readings(SensorType.Temperature), BoardTempNames);
}
