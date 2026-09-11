namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Process-wide access to the persisted <see cref="AppSettings"/>. Loaded once at
/// startup; <see cref="Save"/> validates, writes atomically and raises
/// <see cref="Changed"/> so live consumers pick the new values up without a restart.
/// </summary>
public interface ISettingsStore
{
    /// <summary>The settings currently in force. Immutable snapshot; never null.</summary>
    AppSettings Current { get; }

    /// <summary>Clamp, persist atomically and raise <see cref="Changed"/>. Never throws for bad values.</summary>
    void Save(AppSettings settings);

    /// <summary>Raised after a successful <see cref="Save"/>, carrying the clamped result.</summary>
    event Action<AppSettings>? Changed;
}
