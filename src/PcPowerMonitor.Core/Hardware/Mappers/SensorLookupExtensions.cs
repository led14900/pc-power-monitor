using System.Globalization;
using LibreHardwareMonitor.Hardware;

namespace PcPowerMonitor.Core.Hardware.Mappers;

/// <summary>
/// Small bridge helpers that turn LibreHardwareMonitor <see cref="ISensor"/> objects
/// into the plain <c>(string Name, double? Value)</c> tuples used by
/// <see cref="SensorNameMatcher"/>. Keeps the mappers short and LHM-agnostic.
/// </summary>
internal static class SensorLookupExtensions
{
    /// <summary>All sensors of a given type as name/value tuples (value is float? widened to double?).</summary>
    public static List<(string Name, double? Value)> Readings(this IHardware hw, SensorType type)
    {
        var list = new List<(string Name, double? Value)>();
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == type)
                list.Add((s.Name, (double?)s.Value));
        }
        return list;
    }

    /// <summary>First sensor matching type + exact (case-insensitive) name, or null.</summary>
    public static double? FirstValue(this IHardware hw, SensorType type, string name)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == type && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                return s.Value;
        }
        return null;
    }

    /// <summary>Largest non-null value among sensors of a given type, or null.</summary>
    public static double? MaxValue(this IHardware hw, SensorType type)
    {
        double? max = null;
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != type || s.Value is not float v)
                continue;
            if (max is null || v > max.Value)
                max = v;
        }
        return max;
    }

    /// <summary>Formats a sensor value for the diagnostics dump.</summary>
    public static string FormatValue(this ISensor sensor) =>
        sensor.Value is float v ? v.ToString("0.###", CultureInfo.InvariantCulture) : "—";
}
