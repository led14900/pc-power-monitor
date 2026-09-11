namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Windows auto-start preferences. <see cref="AutoStartEnabled"/> mirrors whether the
/// scheduled task exists (the Settings screen calls <c>IAutoStartManager</c> to
/// reconcile it); <see cref="StartMinimized"/> makes an auto-start launch stay in tray.
/// </summary>
public sealed record StartupSettings
{
    public bool AutoStartEnabled { get; init; }

    public bool StartMinimized { get; init; } = true;
}
