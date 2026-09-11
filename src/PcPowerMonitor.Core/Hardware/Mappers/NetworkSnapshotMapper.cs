using LibreHardwareMonitor.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>
/// Maps one <see cref="HardwareType.Network"/> adapter to a <see cref="NetworkSnapshot"/>.
/// LibreHardwareMonitor reports throughput in bytes/s; we convert to KB/s.
/// </summary>
internal static class NetworkSnapshotMapper
{
    private const double BytesPerKb = 1024.0;

    public static NetworkSnapshot Map(IHardware hw)
    {
        var up = hw.FirstValue(SensorType.Throughput, "Upload Speed");
        var down = hw.FirstValue(SensorType.Throughput, "Download Speed");

        return new NetworkSnapshot(
            hw.Name,
            up is double u ? u / BytesPerKb : null,
            down is double d ? d / BytesPerKb : null);
    }
}
