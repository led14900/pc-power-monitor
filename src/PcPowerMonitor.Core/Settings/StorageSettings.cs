namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Raw-sample retention configuration. Read live by <c>RetentionService</c> on every
/// pass. User-editable, so the value is untrusted — <see cref="SettingsValidator"/>
/// clamps it to 1-365 before it ever reaches a SQL DELETE.
/// </summary>
public sealed record StorageSettings
{
    /// <summary>Days of raw samples to keep. Valid range 1-365; default 90.</summary>
    public int RetentionDays { get; init; } = 90;
}
