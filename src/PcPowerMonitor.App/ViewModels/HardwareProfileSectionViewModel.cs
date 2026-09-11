using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Cấu hình phần cứng" group of the Settings screen. Edits map 1:1 onto the Core
/// <see cref="HardwareProfile"/> record; <see cref="BaselinePreviewW"/> is recomputed
/// live from <see cref="BaselinePowerCalculator"/> whenever any field changes. The
/// "Dò thông số bằng AI" flow (provider-agnostic <c>IAiClient</c> call via
/// <c>AiClientFactory</c>, diff dialog, apply) lives in the
/// <c>HardwareProfileSectionViewModel.Lookup.cs</c> partial to keep this file under
/// the 200-line guideline.
/// </summary>
public sealed partial class HardwareProfileSectionViewModel : ObservableObject
{
    private readonly IHardwareSensorReader _sensorReader;
    private readonly ISettingsStore _store;
    private readonly AiSectionViewModel _aiSection;
    private readonly ILogger<HardwareProfileSectionViewModel>? _log;

    public IReadOnlyList<string> RamTypeOptions { get; } = new[] { "DDR4", "DDR5" };

    public IReadOnlyList<PsuRating> PsuRatingOptions { get; } = Enum.GetValues<PsuRating>();

    [ObservableProperty] private double cpuTdpW = 65;
    [ObservableProperty] private double gpuTdpW;
    [ObservableProperty] private int ramSticks = 2;
    [ObservableProperty] private string ramType = "DDR4";
    [ObservableProperty] private int ssdCount = 1;
    [ObservableProperty] private int hddCount;
    [ObservableProperty] private int fanCount = 3;
    [ObservableProperty] private double motherboardW = 60;
    [ObservableProperty] private double peripheralW = 3;
    [ObservableProperty] private int psuWattage = 650;
    [ObservableProperty] private PsuRating psuRating = PsuRating.Bronze;
    [ObservableProperty] private double baselinePreviewW;

    public HardwareProfileSectionViewModel(
        IHardwareSensorReader sensorReader,
        ISettingsStore store,
        AiSectionViewModel aiSection,
        ILogger<HardwareProfileSectionViewModel>? log = null)
    {
        _sensorReader = sensorReader ?? throw new ArgumentNullException(nameof(sensorReader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _aiSection = aiSection ?? throw new ArgumentNullException(nameof(aiSection));
        _log = log;

        // Same AiSectionViewModel instance (DI singleton) as bound in the "Cấu hình AI"
        // section — the lookup button must react live as the user saves/clears the key.
        _aiSection.PropertyChanged += OnAiSectionPropertyChanged;
        RefreshLookupAvailability();

        Recompute();
    }

    public void Load(HardwareProfile p)
    {
        ArgumentNullException.ThrowIfNull(p);
        CpuTdpW = p.CpuTdpW;
        GpuTdpW = p.GpuTdpW;
        RamSticks = p.RamSticks;
        RamType = p.RamType;
        SsdCount = p.SsdCount;
        HddCount = p.HddCount;
        FanCount = p.FanCount;
        MotherboardW = p.MotherboardW;
        PeripheralW = p.PeripheralW;
        PsuWattage = p.PsuWattage;
        PsuRating = p.PsuRating;
        Recompute();
    }

    public HardwareProfile ToProfile() => new()
    {
        CpuTdpW = CpuTdpW,
        GpuTdpW = GpuTdpW,
        RamSticks = RamSticks,
        RamType = RamType,
        SsdCount = SsdCount,
        HddCount = HddCount,
        FanCount = FanCount,
        MotherboardW = MotherboardW,
        PeripheralW = PeripheralW,
        PsuWattage = PsuWattage,
        PsuRating = PsuRating,
    };

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(BaselinePreviewW)) Recompute();
    }

    private void Recompute()
    {
        try { BaselinePreviewW = Math.Round(BaselinePowerCalculator.Calculate(ToProfile()), 1); }
        catch (Exception) { BaselinePreviewW = 0d; }
    }
}
