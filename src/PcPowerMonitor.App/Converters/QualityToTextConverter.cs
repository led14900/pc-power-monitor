using System.Globalization;
using System.Windows.Data;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.App.Converters;

/// <summary>
/// <see cref="EstimationQuality"/> to its Vietnamese label. The estimate badge is
/// always shown (transparency requirement), so an unknown value still yields text.
/// </summary>
public sealed class QualityToTextConverter : IValueConverter
{
    public static string ToText(EstimationQuality quality) => quality switch
    {
        EstimationQuality.Measured => "Đo được",
        EstimationQuality.Mixed => "Hỗn hợp",
        _ => "Ước tính",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstimationQuality q ? ToText(q) : "Ước tính";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
