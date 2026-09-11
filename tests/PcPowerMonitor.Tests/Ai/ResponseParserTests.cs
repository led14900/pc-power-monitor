using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Ai;

public sealed class ResponseParserTests
{
    [Fact]
    public void Parse_extracts_valid_json_block()
    {
        var response = """
            Based on my research, here are the specs:

            ```json
            {
              "cpuTdpW": 35,
              "gpuTdpW": 0,
              "ramSticks": 2,
              "ramType": "DDR4",
              "ssdCount": 2,
              "hddCount": 0,
              "fanCount": 1,
              "motherboardW": 10,
              "psuWattage": 350
            }
            ```

            Sources: Intel ARK, etc.
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        Assert.Equal(35, result.Profile.CpuTdpW);
        Assert.Equal(0, result.Profile.GpuTdpW);
        Assert.Equal(2, result.Profile.RamSticks);
        Assert.Equal(350, result.Profile.PsuWattage);
    }

    [Fact]
    public void Parse_returns_false_when_no_json_block()
    {
        var response = "I couldn't find the information you requested.";

        var result = ResponseParser.Parse(response);

        Assert.False(result.ParsedSuccessfully);
        Assert.Equal(response, result.RawResponse);
    }

    [Fact]
    public void Parse_returns_false_for_malformed_json()
    {
        var response = """
            ```json
            { this is not valid json }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.False(result.ParsedSuccessfully);
    }

    [Fact]
    public void Parse_returns_false_for_empty_response()
    {
        var result = ResponseParser.Parse(string.Empty);

        Assert.False(result.ParsedSuccessfully);
        Assert.Equal(string.Empty, result.RawResponse);
    }

    [Fact]
    public void Parse_succeeds_on_psu_rating_as_string()
    {
        // PromptBuilder's JSON template instructs Gemini to return psuRating as a STRING
        // ("Bronze"/"Silver"/"Gold"/...). Regression test for a real bug found during
        // Phase 5 testing: SerializerOptions originally lacked JsonStringEnumConverter,
        // so every real, well-formed Gemini response failed to parse. Fixed in
        // ResponseParser.cs — this asserts the fix holds.
        var response = """
            ```json
            { "cpuTdpW": 65, "psuRating": "Gold" }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        Assert.Equal(PsuRating.Gold, result.Profile.PsuRating);
    }

    [Fact]
    public void Parse_clamps_cpu_tdp_above_max_to_1000()
    {
        var response = """
            ```json
            { "cpuTdpW": 99999 }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        Assert.Equal(1000, result.Profile.CpuTdpW);
    }

    [Fact]
    public void Parse_clamps_non_positive_psu_wattage_to_default_650()
    {
        var response = """
            ```json
            { "psuWattage": -100 }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        Assert.Equal(650, result.Profile.PsuWattage);
    }

    [Fact]
    public void Parse_preserves_raw_response_on_success()
    {
        var response = "Full response with ```json { \"cpuTdpW\": 65 } ``` and more text";

        var result = ResponseParser.Parse(response);

        Assert.Equal(response, result.RawResponse);
    }

    [Fact]
    public void Parse_preserves_raw_response_on_failure()
    {
        var response = "No json block anywhere in this text.";

        var result = ResponseParser.Parse(response);

        Assert.Equal(response, result.RawResponse);
    }

    [Fact]
    public void Parse_handles_extra_json_blocks_by_taking_first_match()
    {
        var response = """
            ```json
            { "cpuTdpW": 10 }
            ```
            Some more reasoning...
            ```json
            { "cpuTdpW": 45 }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        // Regex is non-greedy so it matches the FIRST ```json block, not the last.
        Assert.Equal(10, result.Profile.CpuTdpW);
    }

    [Fact]
    public void Parse_ignores_unknown_json_properties()
    {
        var response = """
            ```json
            { "cpuTdpW": 65, "unknownField": "whatever" }
            ```
            """;

        var result = ResponseParser.Parse(response);

        Assert.True(result.ParsedSuccessfully);
        Assert.Equal(65, result.Profile.CpuTdpW);
    }
}
