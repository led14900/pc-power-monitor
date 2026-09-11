using System.Globalization;
using System.Windows.Data;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// Formats the "So tháng trước" cell from <c>[DeltaPercent, DeltaVnd]</c> into
/// e.g. <c>+12,3% (+45.000 đ)</c>. Shows an em dash when there is no comparable
/// previous month (percent is null).
/// </summary>
public sealed class MonthDeltaConverter : IMultiValueConverter
{
    private const string EmptyText = "—";
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2 || values[0] is not double percent
            || double.IsNaN(percent) || double.IsInfinity(percent))
            return EmptyText;

        var vnd = values[1] is double v && double.IsFinite(v) ? v : 0d;
        var percentText = (percent >= 0 ? "+" : "") + percent.ToString("N1", Vi) + "%";
        var vndText = (vnd >= 0 ? "+" : "-") +
            Math.Round(Math.Abs(vnd), MidpointRounding.AwayFromZero).ToString("N0", Vi) + " đ";
        return $"{percentText} ({vndText})";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
