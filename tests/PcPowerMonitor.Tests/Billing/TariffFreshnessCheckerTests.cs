using PcPowerMonitor.Core.Billing;

namespace PcPowerMonitor.Tests.Billing;

/// <summary>
/// Tests for <see cref="TariffFreshnessChecker"/>: tariff staleness detection
/// based on <see cref="TariffSettings.EffectiveFrom"/> age. Tariffs older than
/// 365 days are flagged as stale (EVN revises prices roughly annually).
/// </summary>
public sealed class TariffFreshnessCheckerTests
{
    [Fact]
    public void Is_stale_returns_false_when_tariff_effective_from_today()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var tariff = new TariffSettings { EffectiveFrom = today };

        var isStale = TariffFreshnessChecker.IsStale(tariff, today);

        Assert.False(isStale);
    }

    [Fact]
    public void Is_stale_returns_false_when_tariff_is_364_days_old()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var effectiveFrom = today.AddDays(-364);
        var tariff = new TariffSettings { EffectiveFrom = effectiveFrom };

        var isStale = TariffFreshnessChecker.IsStale(tariff, today);

        Assert.False(isStale);
    }

    [Fact]
    public void Is_stale_returns_true_when_tariff_is_exactly_366_days_old()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var effectiveFrom = today.AddDays(-366);
        var tariff = new TariffSettings { EffectiveFrom = effectiveFrom };

        var isStale = TariffFreshnessChecker.IsStale(tariff, today);

        Assert.True(isStale);
    }

    [Fact]
    public void Is_stale_returns_true_when_tariff_is_many_years_old()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var effectiveFrom = today.AddYears(-2);
        var tariff = new TariffSettings { EffectiveFrom = effectiveFrom };

        var isStale = TariffFreshnessChecker.IsStale(tariff, today);

        Assert.True(isStale);
    }

    [Fact]
    public void Check_returns_non_stale_freshness_when_tariff_is_fresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var tariff = new TariffSettings { EffectiveFrom = today };

        var freshness = TariffFreshnessChecker.Check(tariff, today);

        Assert.False(freshness.IsStale);
        Assert.Null(freshness.Message);
    }

    [Fact]
    public void Check_returns_stale_freshness_with_vietnamese_message_when_tariff_is_old()
    {
        var today = DateOnly.Parse("2026-09-11");
        var effectiveFrom = DateOnly.Parse("2024-09-10"); // More than 365 days old
        var tariff = new TariffSettings { EffectiveFrom = effectiveFrom };

        var freshness = TariffFreshnessChecker.Check(tariff, today);

        Assert.True(freshness.IsStale);
        Assert.NotNull(freshness.Message);
        Assert.Contains("lỗi thời", freshness.Message); // Vietnamese: "outdated"
        Assert.Contains("10/09/2024", freshness.Message); // Date in dd/MM/yyyy format (Vietnamese)
    }

    [Fact]
    public void Check_throws_on_null_tariff()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        Assert.Throws<ArgumentNullException>(() => TariffFreshnessChecker.Check(null!, today));
    }

    [Fact]
    public void Is_stale_throws_on_null_tariff()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        Assert.Throws<ArgumentNullException>(() => TariffFreshnessChecker.IsStale(null!, today));
    }
}
