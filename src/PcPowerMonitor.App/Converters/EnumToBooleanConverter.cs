using System.Globalization;
using System.Windows.Data;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// Binds an enum-valued property to a <c>RadioButton.IsChecked</c>. The
/// <c>ConverterParameter</c> is the enum member name (matched case-insensitively) that
/// this particular radio button represents — e.g. two radio buttons bound to the same
/// <c>AiDeviceType</c> property with parameters "Desktop" and "Laptop".
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: true);
    }
}
