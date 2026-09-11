using CommunityToolkit.Mvvm.ComponentModel;
using PcPowerMonitor.Core.Billing;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Giá điện" group of the Settings screen. VAT is edited as a whole-number percent;
/// the effective date as a <see cref="DateTime"/> for the WPF DatePicker. Maps to the
/// Core <see cref="TariffSettings"/> record.
/// </summary>
public sealed partial class TariffSectionViewModel : ObservableObject
{
    [ObservableProperty] private double unitPriceVnd = 3460;
    [ObservableProperty] private double vatPercent = 10;
    [ObservableProperty] private bool includeVat = true;
    [ObservableProperty] private DateTime effectiveFrom = new(2025, 5, 9);
    [ObservableProperty] private string sourceNote = string.Empty;

    public void Load(TariffSettings t)
    {
        ArgumentNullException.ThrowIfNull(t);
        UnitPriceVnd = t.UnitPriceVnd;
        VatPercent = Math.Round(t.VatRate * 100d, 2);
        IncludeVat = t.IncludeVat;
        EffectiveFrom = t.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
        SourceNote = t.SourceNote;
    }

    public TariffSettings ToTariff() => new()
    {
        UnitPriceVnd = UnitPriceVnd,
        VatRate = VatPercent / 100d,
        IncludeVat = IncludeVat,
        EffectiveFrom = DateOnly.FromDateTime(EffectiveFrom),
        SourceNote = SourceNote,
    };
}
