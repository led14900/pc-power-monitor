using PcPowerMonitor.App.ViewModels;
using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Tests.Ai;

namespace PcPowerMonitor.Tests.Integration;

/// <summary>
/// End-to-end AI lookup flow: PromptBuilder -> FakeAiClient -> ResponseParser ->
/// ProfileDiffViewModel. No real network call anywhere — <see cref="FakeAiClient"/>
/// only ever returns a canned string.
/// </summary>
public sealed class AiLookupIntegrationTests
{
    [Fact]
    public async Task Full_flow_with_fake_client_parses_and_builds_diff()
    {
        var fakeClient = new FakeAiClient
        {
            ResponseToReturn = """
                Here are the specs:
                ```json
                { "cpuTdpW": 35, "motherboardW": 10 }
                ```
                """,
        };

        var snapshot = HardwareSnapshot.Empty(SensorAvailability.Full, null);
        var prompt = PromptBuilder.Build(snapshot, "Test Machine");

        var response = await fakeClient.GenerateAsync(prompt);
        var suggestion = ResponseParser.Parse(response);
        var diff = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.True(suggestion.ParsedSuccessfully);
        Assert.Equal(35, suggestion.Profile.CpuTdpW);
        Assert.True(diff.CanApply);
        Assert.Equal(prompt, fakeClient.PromptReceived);
        Assert.Equal(1, fakeClient.CallCount);
    }

    [Fact]
    public async Task Fake_client_never_makes_network_call_and_records_prompt_verbatim()
    {
        var fakeClient = new FakeAiClient { ResponseToReturn = "plain text, no json" };
        var snapshot = SnapshotWithCpu("AMD Ryzen 5 5600X");

        var prompt = PromptBuilder.Build(snapshot, "Custom Build", AiDeviceType.Desktop);
        var response = await fakeClient.GenerateAsync(prompt);

        Assert.Equal(prompt, fakeClient.PromptReceived);
        Assert.Equal("plain text, no json", response);
    }

    [Fact]
    public async Task Full_flow_surfaces_ai_client_exception_without_network_call()
    {
        var fakeClient = new FakeAiClient
        {
            ExceptionToThrow = new AiClientException(401, "Unauthorized"),
        };
        var snapshot = SnapshotWithCpu("Intel Core i7-12700K");
        var prompt = PromptBuilder.Build(snapshot, null);

        var ex = await Assert.ThrowsAsync<AiClientException>(
            () => fakeClient.GenerateAsync(prompt));

        Assert.Contains("Thông tin xác thực Vertex AI không hợp lệ", ex.Message);
        // Prompt is still recorded even though the call "failed" — proves no early bail-out.
        Assert.Equal(prompt, fakeClient.PromptReceived);
    }

    [Fact]
    public async Task Full_flow_with_unparseable_response_yields_non_applicable_diff()
    {
        var fakeClient = new FakeAiClient { ResponseToReturn = "Không tìm thấy thông tin phù hợp." };
        var snapshot = SnapshotWithCpu("Unknown CPU");
        var prompt = PromptBuilder.Build(snapshot, null);

        var response = await fakeClient.GenerateAsync(prompt);
        var suggestion = ResponseParser.Parse(response);
        var diff = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.False(suggestion.ParsedSuccessfully);
        Assert.False(diff.CanApply);
        Assert.NotNull(diff.ErrorMessage);
        Assert.Equal(response, diff.RawResponse);
    }

    private static HardwareSnapshot SnapshotWithCpu(string cpuName) => new(
        DateTimeOffset.UtcNow,
        SensorAvailability.Full,
        null,
        new CpuSnapshot(cpuName, 65, 60, 40, 4000),
        Array.Empty<GpuSnapshot>(),
        new MemorySnapshot(16, 16, 50),
        Array.Empty<DiskSnapshot>(),
        Array.Empty<FanSnapshot>(),
        Array.Empty<NetworkSnapshot>(),
        null);
}
