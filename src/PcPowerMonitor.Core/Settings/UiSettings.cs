namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Small pieces of remembered UI state that should survive a restart.
/// </summary>
public sealed record UiSettings
{
    /// <summary>True once the "still running in tray" balloon has been shown at least once.</summary>
    public bool ClosedToTrayNoticeShown { get; init; }
}
