using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Tests.Fixtures;

/// <summary>
/// Factory for building <see cref="HardwareSnapshot"/> instances with physically-meaningful
/// parameters. No random data — every field is explicitly set to a concrete value.
/// This is a test fixture, not a mock.
/// </summary>
public static class SnapshotFactory
{
    /// <summary>
    /// A typical desktop config: measured CPU/GPU power, good thermal/load data.
    /// </summary>
    public static HardwareSnapshot Desktop(
        double? cpuW = 65,
        double? gpuW = 150,
        double? cpuTempC = 60,
        double? cpuLoadPercent = 40,
        double? gpuLoadPercent = 70)
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = new CpuSnapshot("CPU", cpuW, cpuTempC, cpuLoadPercent, null);
        var gpu = new GpuSnapshot("GPU", gpuW, null, gpuLoadPercent, null, null, null);

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Full,
            AvailabilityReason: null,
            Cpu: cpu,
            Gpus: new[] { gpu },
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }

    /// <summary>
    /// CPU-only system: measured CPU, no discrete GPU.
    /// </summary>
    public static HardwareSnapshot CpuOnly(
        double? cpuW = 45,
        double? cpuTempC = 55,
        double? cpuLoadPercent = 35)
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = new CpuSnapshot("CPU", cpuW, cpuTempC, cpuLoadPercent, null);

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Full,
            AvailabilityReason: null,
            Cpu: cpu,
            Gpus: Array.Empty<GpuSnapshot>(),
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }

    /// <summary>
    /// All sensors null — fallback/estimated scenario.
    /// </summary>
    public static HardwareSnapshot AllNull()
    {
        var now = DateTimeOffset.UtcNow;

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Unavailable,
            AvailabilityReason: "No sensors available",
            Cpu: null,
            Gpus: Array.Empty<GpuSnapshot>(),
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }

    /// <summary>
    /// CPU measured, GPU power null but load available (triggers TDP fallback).
    /// </summary>
    public static HardwareSnapshot CpuMeasuredGpuEstimated(
        double? cpuW = 65,
        double? cpuTempC = 60,
        double? cpuLoadPercent = 50,
        double? gpuLoadPercent = 60)
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = new CpuSnapshot("CPU", cpuW, cpuTempC, cpuLoadPercent, null);
        var gpu = new GpuSnapshot("GPU", null, null, gpuLoadPercent, null, null, null);

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Partial,
            AvailabilityReason: null,
            Cpu: cpu,
            Gpus: new[] { gpu },
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }

    /// <summary>
    /// CPU power null, CPU load available (triggers TDP × load fallback).
    /// </summary>
    public static HardwareSnapshot CpuEstimatedByLoad(
        double? cpuLoadPercent = 50,
        double? cpuTempC = 50)
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = new CpuSnapshot("CPU", null, cpuTempC, cpuLoadPercent, null);

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Partial,
            AvailabilityReason: null,
            Cpu: cpu,
            Gpus: Array.Empty<GpuSnapshot>(),
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }

    /// <summary>
    /// Multi-GPU system: two discrete GPUs, both measured.
    /// </summary>
    public static HardwareSnapshot MultiGpu(
        double? cpuW = 65,
        double? gpu1W = 100,
        double? gpu2W = 80)
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = new CpuSnapshot("CPU", cpuW, 60, 40, null);
        var gpu1 = new GpuSnapshot("GPU1", gpu1W, null, 50, null, null, null);
        var gpu2 = new GpuSnapshot("GPU2", gpu2W, null, 40, null, null, null);

        return new HardwareSnapshot(
            TimestampUtc: now,
            Availability: SensorAvailability.Full,
            AvailabilityReason: null,
            Cpu: cpu,
            Gpus: new[] { gpu1, gpu2 },
            Memory: null,
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: null);
    }
}
