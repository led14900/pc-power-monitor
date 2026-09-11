using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Safe no-op reader for UI development / tests without Administrator rights.
/// It is NOT fake data: every value is null and availability is always
/// <see cref="SensorAvailability.Unavailable"/>. No random numbers, ever.
/// </summary>
public sealed class NullSensorReader : IHardwareSensorReader
{
    public SensorAvailability Availability => SensorAvailability.Unavailable;

    public string? AvailabilityReason => "Chế độ giới hạn (không đọc được sensor).";

    public HardwareSnapshot ReadSnapshot() => HardwareSnapshot.Empty(Availability, AvailabilityReason);

    public IReadOnlyList<string> GetDiagnostics() =>
        new[] { "NullSensorReader — không có sensor nào (chế độ giới hạn)." };

    public void Dispose()
    {
        // Nothing to release.
    }
}
