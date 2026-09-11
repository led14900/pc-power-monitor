using PcPowerMonitor.Core.Hardware;

namespace PcPowerMonitor.Tests.Hardware;

public sealed class SensorNameMatcherTests
{
    private static (string, double?)[] Readings(params (string, double?)[] items) => items;

    [Fact]
    public void Pick_prefers_first_name_in_preference_list()
    {
        var readings = Readings(
            ("CPU Power", 12.0),
            ("Package Power", 65.0),
            ("CPU Package", 70.0));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.CpuPackagePowerNames);

        Assert.NotNull(picked);
        Assert.Equal("CPU Package", picked!.Value.Name);
        Assert.Equal(70.0, picked.Value.Value);
    }

    [Fact]
    public void Pick_respects_preference_order_not_input_order()
    {
        var readings = Readings(
            ("CPU PPT", 88.0),
            ("Package Power", 65.0));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.CpuPackagePowerNames);

        Assert.Equal("Package Power", picked!.Value.Name); // "Package Power" ranks above "CPU PPT"
    }

    [Fact]
    public void Pick_matches_name_case_insensitively()
    {
        var readings = Readings(("cpu package", 55.0));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.CpuPackagePowerNames);

        Assert.Equal("cpu package", picked!.Value.Name);
        Assert.Equal(55.0, picked.Value.Value);
    }

    [Fact]
    public void Pick_falls_back_to_largest_value_when_no_name_matches()
    {
        var readings = Readings(
            ("SoC Power", 5.0),
            ("Random Rail", 41.0),
            ("Other", 12.0));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.CpuPackagePowerNames);

        Assert.Equal("Random Rail", picked!.Value.Name);
        Assert.Equal(41.0, picked.Value.Value);
    }

    [Fact]
    public void Pick_fallback_ignores_null_values()
    {
        var readings = Readings(
            ("A", null),
            ("B", 3.0),
            ("C", null));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.GpuPowerNames);

        Assert.Equal("B", picked!.Value.Name);
    }

    [Fact]
    public void Pick_returns_first_reading_when_all_values_null_and_no_name_match()
    {
        var readings = Readings(("A", null), ("B", null));

        var picked = SensorNameMatcher.Pick(readings, SensorNameMatcher.GpuPowerNames);

        Assert.Equal("A", picked!.Value.Name);
        Assert.Null(picked.Value.Value);
    }

    [Fact]
    public void Pick_returns_null_for_empty_input()
    {
        var picked = SensorNameMatcher.Pick(
            Array.Empty<(string, double?)>(), SensorNameMatcher.CpuPackagePowerNames);

        Assert.Null(picked);
    }

    [Fact]
    public void PickValue_returns_only_the_value()
    {
        var readings = Readings(("GPU Power", 210.5), ("GPU Fan", 1200.0));

        var value = SensorNameMatcher.PickValue(readings, SensorNameMatcher.GpuPowerNames);

        Assert.Equal(210.5, value);
    }

    [Fact]
    public void PickValue_returns_null_for_empty_input()
    {
        var value = SensorNameMatcher.PickValue(
            Array.Empty<(string, double?)>(), SensorNameMatcher.GpuPowerNames);

        Assert.Null(value);
    }
}
