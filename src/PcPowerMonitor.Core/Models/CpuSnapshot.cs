namespace PcPowerMonitor.Core.Models;

/// <summary>
/// Immutable CPU reading. Every numeric field is nullable — a null means the sensor
/// was absent or had no value yet. Never coerce null to 0 (breaks kWh math).
/// </summary>
public sealed record CpuSnapshot(
    string? Name,
    double? PackagePowerW,
    double? PackageTempC,
    double? TotalLoadPercent,
    double? MaxClockMhz);
