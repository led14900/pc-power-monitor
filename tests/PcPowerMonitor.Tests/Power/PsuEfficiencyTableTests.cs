using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class PsuEfficiencyTableTests
{
    [Theory]
    [InlineData(0.20, 0.87)]
    [InlineData(0.50, 0.90)]
    [InlineData(1.00, 0.87)]
    public void Lookup_returns_published_marks_for_gold(double load, double expected)
        => Assert.Equal(expected, PsuEfficiencyTable.Lookup(PsuRating.Gold, load), 4);

    [Fact]
    public void Lookup_interpolates_linearly_between_20_and_50()
    {
        // Gold: 0.87 at 0.20, 0.90 at 0.50 → midpoint 0.35 → 0.885.
        Assert.Equal(0.885, PsuEfficiencyTable.Lookup(PsuRating.Gold, 0.35), 4);
    }

    [Fact]
    public void Lookup_interpolates_linearly_between_50_and_100()
    {
        // Gold: 0.90 at 0.50, 0.87 at 1.00 → 0.75 → 0.885.
        Assert.Equal(0.885, PsuEfficiencyTable.Lookup(PsuRating.Gold, 0.75), 4);
    }

    [Fact]
    public void Lookup_uses_at10_mark_below_ten_percent_load()
    {
        // at10 == at20 - 0.07.
        Assert.Equal(0.80, PsuEfficiencyTable.Lookup(PsuRating.Gold, 0.05), 4);
        Assert.Equal(0.80, PsuEfficiencyTable.Lookup(PsuRating.Gold, 0.10), 4);
    }

    [Fact]
    public void Lookup_clamps_overload_to_the_100_percent_mark()
        => Assert.Equal(0.87, PsuEfficiencyTable.Lookup(PsuRating.Gold, 2.5), 4);

    [Fact]
    public void Lookup_handles_non_finite_load_without_throwing()
    {
        Assert.Equal(0.80, PsuEfficiencyTable.Lookup(PsuRating.Gold, double.NaN), 4);
        Assert.Equal(0.80, PsuEfficiencyTable.Lookup(PsuRating.Gold, double.NegativeInfinity), 4);
    }

    [Theory]
    [InlineData(PsuRating.White)]
    [InlineData(PsuRating.Bronze)]
    [InlineData(PsuRating.Silver)]
    [InlineData(PsuRating.Gold)]
    [InlineData(PsuRating.Platinum)]
    [InlineData(PsuRating.Titanium)]
    public void Lookup_result_is_always_within_clamp_bounds(PsuRating rating)
    {
        foreach (var load in new[] { -1d, 0d, 0.01, 0.1, 0.2, 0.45, 0.5, 0.8, 1.0, 3.0 })
        {
            var eff = PsuEfficiencyTable.Lookup(rating, load);
            Assert.InRange(eff, PsuEfficiencyTable.MinEfficiency, PsuEfficiencyTable.MaxEfficiency);
        }
    }
}
