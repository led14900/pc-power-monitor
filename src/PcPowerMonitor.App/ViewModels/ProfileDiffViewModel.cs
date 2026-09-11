using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// Data context for <see cref="Views.ProfileSuggestionDialog"/>: a field-by-field diff
/// between the currently-saved <see cref="HardwareProfile"/> and the AI-suggested one,
/// plus the raw response text (shown verbatim for transparency) and whether Apply is
/// allowed at all (only when the response actually parsed as valid JSON).
/// </summary>
public sealed class ProfileDiffViewModel
{
    public IReadOnlyList<FieldDiff> Fields { get; }

    public string RawResponse { get; }

    public bool CanApply { get; }

    public string? ErrorMessage { get; }

    public HardwareProfile SuggestedProfile { get; }

    public ProfileDiffViewModel(HardwareProfile current, HardwareProfileSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(suggestion);

        RawResponse = suggestion.RawResponse;
        CanApply = suggestion.ParsedSuccessfully;
        SuggestedProfile = suggestion.Profile;
        ErrorMessage = suggestion.ParsedSuccessfully
            ? null
            : "Không thể phân tích kết quả AI. Xem phản hồi gốc bên dưới.";

        Fields = BuildDiffs(current, suggestion.Profile);
    }

    private static List<FieldDiff> BuildDiffs(HardwareProfile c, HardwareProfile s) => new()
    {
        Diff("CPU TDP", c.CpuTdpW, s.CpuTdpW, "W"),
        Diff("GPU TDP", c.GpuTdpW, s.GpuTdpW, "W"),
        Diff("RAM thanh", c.RamSticks, s.RamSticks, ""),
        Diff("RAM loại", c.RamType, s.RamType, ""),
        Diff("SSD", c.SsdCount, s.SsdCount, ""),
        Diff("HDD", c.HddCount, s.HddCount, ""),
        Diff("Fan", c.FanCount, s.FanCount, ""),
        Diff("Mainboard (tải)", c.MotherboardW, s.MotherboardW, "W"),
        Diff("Mainboard (idle)", c.MotherboardIdleW, s.MotherboardIdleW, "W"),
        Diff("SSD (tải)", c.SsdActiveW, s.SsdActiveW, "W"),
        Diff("SSD (idle)", c.SsdIdleW, s.SsdIdleW, "W"),
        Diff("Fan (tải)", c.FanActiveW, s.FanActiveW, "W"),
        Diff("Fan (idle)", c.FanIdleW, s.FanIdleW, "W"),
        Diff("Fan RPM max", c.FanMaxRpm, s.FanMaxRpm, ""),
        Diff("Ngoại vi", c.PeripheralW, s.PeripheralW, "W"),
        Diff("PSU", c.PsuWattage, s.PsuWattage, "W"),
        Diff("PSU Rating", c.PsuRating.ToString(), s.PsuRating.ToString(), ""),
    };

    private static FieldDiff Diff(string name, double c, double s, string unit)
        => new(name, Format(c, unit), Format(s, unit), unit, Math.Abs(c - s) > 0.01);

    private static FieldDiff Diff(string name, int c, int s, string unit)
        => new(name, $"{c}", $"{s}", unit, c != s);

    private static FieldDiff Diff(string name, string c, string s, string unit)
        => new(name, c, s, unit, !string.Equals(c, s, StringComparison.OrdinalIgnoreCase));

    private static string Format(double v, string unit)
        => v <= 0 ? "(tự tính)" : $"{v:0.#} {unit}".Trim();
}
