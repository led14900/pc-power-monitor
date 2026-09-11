using System.Globalization;
using System.Windows.Data;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// Renders a nullable/NaN <see cref="double"/> as text, or an em dash when there is
/// no value. Never shows "0" for a missing reading. <c>ConverterParameter</c> is an
/// optional numeric format string (default <c>"0.#"</c>); formatting uses vi-VN.
/// </summary>
public sealed class NullableDoubleToDisplayConverter : IValueConverter
{
    public string EmptyText { get; set; } = "—";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double d = value switch
        {
            double dv => dv,
            float fv => fv,
            int iv => iv,
            _ => double.NaN,
        };
        if (value is null || double.IsNaN(d) || double.IsInfinity(d))
            return EmptyText;

        var format = parameter as string;
        if (string.IsNullOrEmpty(format)) format = "0.#";
        return d.ToString(format, CultureInfo.GetCultureInfo("vi-VN"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
