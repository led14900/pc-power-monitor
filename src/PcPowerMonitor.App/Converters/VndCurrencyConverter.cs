using System.Globalization;
using System.Windows.Data;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// Formats a VND amount as e.g. <c>12.345 ₫</c> (vi-VN grouping, no decimals).
/// v1 does not bill monitor power; this only presents an already-computed figure.
/// </summary>
public sealed class VndCurrencyConverter : IValueConverter
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = value switch
        {
            double d => d,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => 0d,
        };
        if (double.IsNaN(amount) || double.IsInfinity(amount)) amount = 0d;
        return string.Format(Vi, "{0:N0} ₫", Math.Round(amount));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
