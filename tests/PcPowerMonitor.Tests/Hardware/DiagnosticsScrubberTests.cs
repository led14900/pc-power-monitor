using PcPowerMonitor.Core.Hardware;

namespace PcPowerMonitor.Tests.Hardware;

public sealed class DiagnosticsScrubberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scrub_returns_placeholder_for_blank(string? input)
    {
        Assert.Equal("?", DiagnosticsScrubber.Scrub(input));
    }

    [Fact]
    public void Scrub_keeps_plain_model_name_unchanged()
    {
        Assert.Equal("Samsung SSD 980 PRO 1TB", DiagnosticsScrubber.Scrub("Samsung SSD 980 PRO 1TB"));
    }

    [Theory]
    [InlineData("WDC WD10EZEX S/N: WD-WCC6Y1234567", "WDC WD10EZEX")]
    [InlineData("Crucial MX500 SN 2140E1234567", "Crucial MX500")]
    [InlineData("Disk Serial Number: 50026B7684F1ABCD", "Disk")]
    public void Scrub_removes_serial_tokens(string input, string expected)
    {
        Assert.Equal(expected, DiagnosticsScrubber.Scrub(input));
    }

    [Fact]
    public void Scrub_collapses_double_spaces_left_by_removal()
    {
        var result = DiagnosticsScrubber.Scrub("Model S/N: ABC123 Rev A");
        Assert.DoesNotContain("  ", result);
        Assert.StartsWith("Model", result);
    }
}
