using PcPowerMonitor.Core.Billing;

namespace PcPowerMonitor.Tests.Billing;

public sealed class ElectricityCostCalculatorTests
{
    private static readonly ElectricityCostCalculator Calc =
        new(new FixedTariffProvider());

    [Fact]
    public void Default_tariff_100_kwh_is_346000_plus_10pct_vat()
    {
        var b = Calc.Calculate(100d, new TariffSettings());

        Assert.Equal(346_000d, b.SubtotalVnd, 6);
        Assert.Equal(34_600d, b.VatVnd, 6);
        Assert.Equal(380_600d, b.TotalVnd, 6);
        Assert.Equal(3460d, b.UnitPrice, 6);
        Assert.Equal(100d, b.KwhTotal, 6);
    }

    [Fact]
    public void Fractional_kwh_keeps_full_precision_no_early_rounding()
    {
        var tariff = new TariffSettings { UnitPriceVnd = 3460d, VatRate = 0.10d };

        var b = Calc.Calculate(0.4567d, tariff);

        // 0.4567 * 3460 * 1.1 with no intermediate rounding.
        Assert.Equal(0.4567d * 3460d * 1.1d, b.TotalVnd, 9);
        Assert.Equal(1738.2002d, b.TotalVnd, 4);
    }

    [Fact]
    public void Negative_or_nonfinite_kwh_yields_zero()
    {
        var neg = Calc.Calculate(-5d, new TariffSettings());
        var nan = Calc.Calculate(double.NaN, new TariffSettings());

        Assert.Equal(0d, neg.TotalVnd);
        Assert.Equal(0d, nan.TotalVnd);
        Assert.Equal(0d, neg.SubtotalVnd);
    }

    [Fact]
    public void Non_positive_price_falls_back_to_tier6_default()
    {
        var b = Calc.Calculate(1d, new TariffSettings { UnitPriceVnd = 0d });

        Assert.Equal(3460d, b.UnitPrice, 6);
        Assert.Equal(3460d, b.SubtotalVnd, 6);
    }

    [Fact]
    public void Vat_rate_is_clamped_to_0_50_range()
    {
        var high = Calc.Calculate(1d, new TariffSettings { UnitPriceVnd = 3460d, VatRate = 5d });
        var low = Calc.Calculate(1d, new TariffSettings { UnitPriceVnd = 3460d, VatRate = -1d });

        Assert.Equal(3460d * 0.5d, high.VatVnd, 6);
        Assert.Equal(0d, low.VatVnd, 6);
    }

    [Fact]
    public void IncludeVat_false_zeroes_the_vat()
    {
        var b = Calc.Calculate(10d, new TariffSettings { IncludeVat = false });

        Assert.Equal(0d, b.VatVnd, 6);
        Assert.Equal(b.SubtotalVnd, b.TotalVnd, 6);
    }
}
