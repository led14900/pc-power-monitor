namespace PcPowerMonitor.Core.Startup;

/// <summary>
/// Turns "start with Windows" on and off. The only implementation drives
/// <c>schtasks.exe</c>; the Settings screen (phase 08) calls this.
/// </summary>
public interface IAutoStartManager
{
    /// <summary>True if the auto-start scheduled task currently exists.</summary>
    bool IsEnabled();

    /// <summary>Create / overwrite the auto-start task. Throws <see cref="AutoStartException"/> on failure.</summary>
    void Enable();

    /// <summary>Remove the auto-start task. A missing task is not an error.</summary>
    void Disable();
}
