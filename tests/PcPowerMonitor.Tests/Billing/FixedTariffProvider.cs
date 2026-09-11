using PcPowerMonitor.Core.Billing;

namespace PcPowerMonitor.Tests.Billing;

/// <summary>Test double: always returns the tariff it was constructed with.</summary>
internal sealed class FixedTariffProvider : ITariffProvider
{
    public FixedTariffProvider(TariffSettings? tariff = null) => Current = tariff ?? new TariffSettings();

    public TariffSettings Current { get; }
}
