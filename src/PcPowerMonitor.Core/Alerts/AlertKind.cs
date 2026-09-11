namespace PcPowerMonitor.Core.Alerts;

/// <summary>The three things the app can raise a threshold alert about.</summary>
public enum AlertKind
{
    CpuTemp,
    GpuTemp,
    Power,
}
