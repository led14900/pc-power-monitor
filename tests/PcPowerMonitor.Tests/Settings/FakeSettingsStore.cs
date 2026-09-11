using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Tests.Settings;

/// <summary>
/// In-memory <see cref="ISettingsStore"/> for tests: no file IO, mutable snapshot,
/// raises <see cref="Changed"/> on <see cref="Save"/> just like the real store.
/// </summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public FakeSettingsStore(AppSettings? initial = null) => Current = initial ?? new AppSettings();

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    public void Save(AppSettings settings)
    {
        Current = settings ?? throw new ArgumentNullException(nameof(settings));
        Changed?.Invoke(Current);
    }
}
