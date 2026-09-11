namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Threshold-alert configuration read by <c>AlertService</c> on every tick. All three
/// thresholds and the cooldown are user-editable and therefore clamped by
/// <see cref="SettingsValidator"/> (temp 40-110, power 50-2000, cooldown 1-240).
/// </summary>
public sealed record AlertSettings
{
    /// <summary>Master switch. When false <c>AlertService</c> skips evaluation entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>CPU package temperature alarm point in °C.</summary>
    public double CpuTempC { get; init; } = 90;

    /// <summary>Hottest-GPU temperature alarm point in °C.</summary>
    public double GpuTempC { get; init; } = 90;

    /// <summary>Estimated wall-power alarm point in watts.</summary>
    public double PowerW { get; init; } = 500;

    /// <summary>Minimum minutes between two notifications of the same kind.</summary>
    public int CooldownMinutes { get; init; } = 10;
}
