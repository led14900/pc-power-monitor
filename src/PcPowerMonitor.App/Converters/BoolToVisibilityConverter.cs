using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// Bool to <see cref="Visibility"/>. Pass <c>ConverterParameter=invert</c> to flip
/// the sense (true → Collapsed). Collapsed (not Hidden) so hidden panels take no space.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
