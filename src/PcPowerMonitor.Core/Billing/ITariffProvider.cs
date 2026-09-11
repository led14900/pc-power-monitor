namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// Supplies the tariff currently in force. Phase 07 backs this with
/// <c>IOptionsMonitor&lt;TariffSettings&gt;</c> (defaults only); phase 08 rebinds the same
/// seam to the on-disk JSON settings store without touching any consumer.
/// </summary>
public interface ITariffProvider
{
    /// <summary>The tariff to use for any cost computed "now". Never null.</summary>
    TariffSettings Current { get; }
}
