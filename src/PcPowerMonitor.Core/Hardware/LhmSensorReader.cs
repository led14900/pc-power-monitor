using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Hardware.Mappers;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// LibreHardwareMonitor adapter. Thin on purpose: opens the driver once (guarded),
/// serialises every access behind a lock (LHM is not thread-safe) and never lets
/// an exception escape. Driver failure => degraded <see cref="SensorAvailability.Unavailable"/>.
/// </summary>
public sealed class LhmSensorReader : IHardwareSensorReader
{
    private readonly ILogger<LhmSensorReader> _log;
    private readonly object _gate = new();
    private readonly LhmUpdateVisitor _visitor = new();
    private Computer? _computer;
    private bool _disposed;

    public SensorAvailability Availability { get; private set; } = SensorAvailability.Unavailable;
    public string? AvailabilityReason { get; private set; }

    public LhmSensorReader(ILogger<LhmSensorReader> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsPsuEnabled = true,
                IsBatteryEnabled = true,
            };
            computer.Open();
            computer.Accept(_visitor);
            _computer = computer;

            var hasPower = HasCpuOrGpuPowerSensor(computer);
            Availability = hasPower ? SensorAvailability.Full : SensorAvailability.Partial;
            AvailabilityReason = hasPower
                ? null
                : "Không tìm thấy sensor công suất — sẽ ước lượng theo TDP × Load.";

            LogStartupDiagnostics();
        }
        catch (Exception ex)
        {
            Availability = SensorAvailability.Unavailable;
            AvailabilityReason =
                "Không mở được LibreHardwareMonitor (thiếu quyền Administrator hoặc driver bị chặn): "
                + ex.Message;
            _log.LogError(ex, "LibreHardwareMonitor.Open() thất bại — chạy chế độ giới hạn (Unavailable).");
        }
    }

    public HardwareSnapshot ReadSnapshot()
    {
        lock (_gate)
        {
            if (_computer is null)
                return HardwareSnapshot.Empty(Availability, AvailabilityReason);

            try
            {
                _computer.Accept(_visitor);
                return HardwareSnapshotBuilder.Build(_computer, Availability, AvailabilityReason);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "ReadSnapshot() lỗi — trả snapshot rỗng.");
                return HardwareSnapshot.Empty(Availability, AvailabilityReason);
            }
        }
    }

    public IReadOnlyList<string> GetDiagnostics()
    {
        lock (_gate)
        {
            if (_computer is null)
            {
                return new[]
                {
                    "LibreHardwareMonitor không khả dụng: " + (AvailabilityReason ?? "không rõ lý do"),
                };
            }

            var lines = new List<string>();
            foreach (var hw in HardwareSnapshotBuilder.EnumerateAll(_computer))
            {
                var name = DiagnosticsScrubber.Scrub(hw.Name);
                foreach (var s in hw.Sensors)
                    lines.Add($"{hw.HardwareType} | {name} | {s.SensorType} | {s.Name} = {s.FormatValue()}");
            }
            return lines;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (_gate)
        {
            try
            {
                _computer?.Close();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "computer.Close() lỗi khi Dispose.");
            }
            _computer = null;
        }
    }

    private void LogStartupDiagnostics()
    {
        try
        {
            var diag = GetDiagnostics();
            _log.LogInformation(
                "LHM diagnostics — Availability={Availability}, {Count} sensor:", Availability, diag.Count);
            foreach (var line in diag)
                _log.LogInformation("  {SensorLine}", line);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không dump được diagnostics khi khởi tạo.");
        }
    }

    private static bool HasCpuOrGpuPowerSensor(Computer computer)
    {
        foreach (var hw in HardwareSnapshotBuilder.EnumerateAll(computer))
        {
            if (hw.HardwareType is not (HardwareType.Cpu or HardwareType.GpuNvidia
                or HardwareType.GpuAmd or HardwareType.GpuIntel))
                continue;

            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == SensorType.Power)
                    return true;
            }
        }
        return false;
    }
}
