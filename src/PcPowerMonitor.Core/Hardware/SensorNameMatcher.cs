namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Pure sensor-selection helpers. Vendors name the "package power" / "package temp"
/// sensor differently (Intel vs AMD vs NVIDIA), so we try a preferred-name list
/// first and fall back to the reading with the largest value.
///
/// Everything here takes plain <c>(string Name, double? Value)</c> tuples so it is
/// unit-testable without real hardware or LibreHardwareMonitor.
/// </summary>
public static class SensorNameMatcher
{
    /// <summary>Preferred CPU package-power sensor names, most-specific first.</summary>
    public static readonly string[] CpuPackagePowerNames =
        { "CPU Package", "Package Power", "Package", "CPU PPT", "CPU Power" };

    /// <summary>Preferred GPU total-power sensor names.</summary>
    public static readonly string[] GpuPowerNames =
        { "GPU Power", "GPU Package", "GPU PPT", "GPU Total", "GPU SoC" };

    /// <summary>Preferred CPU package-temperature sensor names.</summary>
    public static readonly string[] CpuPackageTempNames =
        { "CPU Package", "Core (Tctl/Tdie)", "Core Max", "Core Average", "CPU Cores" };

    /// <summary>Preferred GPU core-temperature sensor names.</summary>
    public static readonly string[] GpuTempNames =
        { "GPU Core", "GPU Hot Spot", "GPU", "GPU Temperature" };

    /// <summary>
    /// Picks the "right" reading: first case-insensitive match against
    /// <paramref name="preferredNames"/> (in order), else the reading with the
    /// largest non-null value, else the first reading, else null when empty.
    /// </summary>
    public static (string Name, double? Value)? Pick(
        IEnumerable<(string Name, double? Value)> readings,
        IReadOnlyList<string> preferredNames)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(preferredNames);

        var list = readings as IReadOnlyList<(string Name, double? Value)> ?? readings.ToList();
        if (list.Count == 0)
            return null;

        foreach (var pref in preferredNames)
        {
            foreach (var r in list)
            {
                if (string.Equals(r.Name, pref, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
        }

        (string Name, double? Value)? best = null;
        double? bestValue = null;
        foreach (var r in list)
        {
            if (r.Value is not double v)
                continue;
            if (bestValue is null || v > bestValue.Value)
            {
                bestValue = v;
                best = r;
            }
        }

        return best ?? list[0];
    }

    /// <summary>Convenience: <see cref="Pick"/> then take just the value (may be null).</summary>
    public static double? PickValue(
        IEnumerable<(string Name, double? Value)> readings,
        IReadOnlyList<string> preferredNames)
        => Pick(readings, preferredNames)?.Value;
}
