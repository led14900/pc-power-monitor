using CommunityToolkit.Mvvm.ComponentModel;
using PcPowerMonitor.App.Converters;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Core.Storage.Dtos;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Tổng quan" tab numbers. Per-tick updates move the live watt + quality badge and
/// nudge <see cref="TodayKwh"/> by each tick's increment; the authoritative daily
/// totals are re-read from <see cref="IEnergyQueryService.GetTodaySummary"/> on a
/// 60s cadence (see <see cref="MainViewModel"/>). Callers marshal onto the UI thread.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly ICostCalculator _cost;

    public DashboardViewModel(ICostCalculator cost) => _cost = cost;

    [ObservableProperty] private double currentWatt;
    [ObservableProperty] private double todayKwh;
    [ObservableProperty] private double todayCostVnd;
    [ObservableProperty] private double avgWatt;
    [ObservableProperty] private double peakWatt;
    [ObservableProperty] private string uptimeText = "0 phút";
    [ObservableProperty] private string qualityText = "Ước tính";
    [ObservableProperty] private string qualityErrorText = "±30%";

    /// <summary>Move the live figures from the newest tick. <paramref name="deltaKwh"/> is
    /// the energy folded in since the previous dashboard update.</summary>
    public void ApplyTick(PowerEstimate estimate, double deltaKwh)
    {
        CurrentWatt = estimate.WallW;
        QualityText = QualityToTextConverter.ToText(estimate.Quality);
        QualityErrorText = "±" + (PowerEstimate.ErrorFraction(estimate.Quality) * 100d)
            .ToString("0") + "%";
        if (deltaKwh > 0d) TodayKwh += deltaKwh;
    }

    /// <summary>Reset the daily aggregates to the DB truth (called every 60s).</summary>
    public void ApplySummary(TodaySummary summary)
    {
        TodayKwh = summary.Kwh;
        AvgWatt = summary.AvgW;
        PeakWatt = summary.MaxW;
        UptimeText = FormatUptime(summary.UptimeSeconds);
        TodayCostVnd = _cost.Calculate(summary.Kwh, summary.Date).CostVnd;
    }

    private static string FormatUptime(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;
        return hours > 0 ? $"{hours} giờ {minutes} phút" : $"{minutes} phút";
    }
}
