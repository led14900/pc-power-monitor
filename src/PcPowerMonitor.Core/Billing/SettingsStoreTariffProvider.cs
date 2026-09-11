using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// <see cref="ITariffProvider"/> over the unified JSON settings store (phase 08).
/// Replaces the phase-07 options-backed provider so cost math reflects the user's
/// edits live — <see cref="Current"/> just reads <c>ISettingsStore.Current.Tariff</c>,
/// which the store already validated.
/// </summary>
public sealed class SettingsStoreTariffProvider : ITariffProvider
{
    private readonly ISettingsStore _store;

    public SettingsStoreTariffProvider(ISettingsStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public TariffSettings Current => _store.Current.Tariff;
}
