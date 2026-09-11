using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Tests.Ai;

public sealed class PromptBuilderTests
{
    [Fact]
    public void Build_includes_cpu_name()
    {
        var snapshot = CreateSnapshot(cpuName: "Intel Core i5-6400T");

        var prompt = PromptBuilder.Build(snapshot, machineModel: null);

        Assert.Contains("Intel Core i5-6400T", prompt);
    }

    [Fact]
    public void Build_includes_machine_model_when_provided()
    {
        var snapshot = CreateSnapshot();

        var prompt = PromptBuilder.Build(snapshot, "Lenovo ThinkCentre M700 Tiny");

        Assert.Contains("Lenovo ThinkCentre M700 Tiny", prompt);
    }

    [Fact]
    public void Build_handles_null_cpu()
    {
        var snapshot = CreateSnapshot(cpuName: null);

        var prompt = PromptBuilder.Build(snapshot, null);

        Assert.DoesNotContain("CPU:", prompt);
    }

    [Fact]
    public void Build_includes_json_template_with_all_field_names()
    {
        var snapshot = CreateSnapshot();

        var prompt = PromptBuilder.Build(snapshot, null);

        Assert.Contains("```json", prompt);
        Assert.Contains("cpuTdpW", prompt);
        Assert.Contains("gpuTdpW", prompt);
        Assert.Contains("ramSticks", prompt);
        Assert.Contains("ramType", prompt);
        Assert.Contains("ssdCount", prompt);
        Assert.Contains("hddCount", prompt);
        Assert.Contains("fanCount", prompt);
        Assert.Contains("motherboardW", prompt);
        Assert.Contains("motherboardIdleW", prompt);
        Assert.Contains("ssdActiveW", prompt);
        Assert.Contains("ssdIdleW", prompt);
        Assert.Contains("fanActiveW", prompt);
        Assert.Contains("fanIdleW", prompt);
        Assert.Contains("fanMaxRpm", prompt);
        Assert.Contains("peripheralW", prompt);
        Assert.Contains("psuWattage", prompt);
        Assert.Contains("psuRating", prompt);
    }

    [Fact]
    public void Build_desktop_device_type_uses_per_component_framing()
    {
        var snapshot = CreateSnapshot();

        var prompt = PromptBuilder.Build(snapshot, null, AiDeviceType.Desktop);

        Assert.Contains("PC để bàn", prompt);
        Assert.Contains("MỘT CÁCH ĐỘC LẬP", prompt);
    }

    [Fact]
    public void Build_laptop_device_type_uses_whole_unit_framing()
    {
        var snapshot = CreateSnapshot();

        var prompt = PromptBuilder.Build(snapshot, null, AiDeviceType.Laptop);

        Assert.Contains("chuyên gia phần cứng laptop", prompt);
        Assert.Contains("CỐ ĐỊNH", prompt);
    }

    [Fact]
    public void Build_desktop_and_laptop_produce_different_prompt_text()
    {
        var snapshot = CreateSnapshot();

        var desktopPrompt = PromptBuilder.Build(snapshot, "Some Model", AiDeviceType.Desktop);
        var laptopPrompt = PromptBuilder.Build(snapshot, "Some Model", AiDeviceType.Laptop);

        Assert.NotEqual(desktopPrompt, laptopPrompt);
    }

    [Fact]
    public void Build_laptop_includes_machine_model_line_once()
    {
        var snapshot = CreateSnapshot();

        var prompt = PromptBuilder.Build(snapshot, "Dell XPS 15 9530", AiDeviceType.Laptop);

        // Component list must not duplicate the model line already printed by the laptop section.
        var occurrences = prompt.Split("Dell XPS 15 9530").Length - 1;
        Assert.Equal(1, occurrences);
    }

    private static HardwareSnapshot CreateSnapshot(string? cpuName = "Test CPU")
        => new(
            DateTimeOffset.UtcNow,
            SensorAvailability.Full,
            null,
            cpuName is null ? null : new CpuSnapshot(cpuName, 50, 60, 50, 3000),
            Array.Empty<GpuSnapshot>(),
            new MemorySnapshot(8, 8, 50),
            Array.Empty<DiskSnapshot>(),
            Array.Empty<FanSnapshot>(),
            Array.Empty<NetworkSnapshot>(),
            null);
}
