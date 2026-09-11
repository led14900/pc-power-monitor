using System.Globalization;

namespace PcPowerMonitor.Core.Billing;

/// <summary>Outcome of a staleness check: <see cref="Message"/> is set only when stale.</summary>
public readonly record struct TariffFreshness(bool IsStale, string? Message);

/// <summary>
/// Flags a tariff whose <see cref="TariffSettings.EffectiveFrom"/> is more than a year old —
/// EVN revises prices roughly annually, so an older config is probably wrong. Pure; the
/// caller passes "today" so tests stay deterministic.
/// </summary>
public static class TariffFreshnessChecker
{
    public const int MaxAgeDays = 365;

    public static bool IsStale(TariffSettings tariff, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        return today.DayNumber - tariff.EffectiveFrom.DayNumber > MaxAgeDays;
    }

    public static TariffFreshness Check(TariffSettings tariff, DateOnly today)
    {
        if (!IsStale(tariff, today)) return new TariffFreshness(false, null);

        var message = string.Format(
            CultureInfo.GetCultureInfo("vi-VN"),
            "Đơn giá đã cấu hình từ {0:dd/MM/yyyy}, có thể lỗi thời. Kiểm tra hóa đơn EVN.",
            tariff.EffectiveFrom);
        return new TariffFreshness(true, message);
    }
}
