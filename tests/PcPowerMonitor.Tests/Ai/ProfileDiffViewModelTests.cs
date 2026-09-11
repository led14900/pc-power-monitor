using PcPowerMonitor.App.ViewModels;
using PcPowerMonitor.Core.Ai;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Ai;

public sealed class ProfileDiffViewModelTests
{
    [Fact]
    public void Fields_marked_changed_when_values_differ()
    {
        var current = new HardwareProfile { CpuTdpW = 65 };
        var suggested = new HardwareProfile { CpuTdpW = 35 };
        var suggestion = new HardwareProfileSuggestion(suggested, "raw", true);

        var vm = new ProfileDiffViewModel(current, suggestion);

        var cpuField = vm.Fields.First(f => f.DisplayName == "CPU TDP");
        Assert.True(cpuField.Changed);
        Assert.Equal("65 W", cpuField.CurrentValue);
        Assert.Equal("35 W", cpuField.SuggestedValue);
    }

    [Fact]
    public void Fields_not_changed_when_values_match()
    {
        var profile = new HardwareProfile { RamSticks = 2 };
        var suggestion = new HardwareProfileSuggestion(profile, "raw", true);

        var vm = new ProfileDiffViewModel(profile, suggestion);

        var ramField = vm.Fields.First(f => f.DisplayName == "RAM thanh");
        Assert.False(ramField.Changed);
    }

    [Fact]
    public void String_field_changed_is_case_insensitive_comparison()
    {
        var current = new HardwareProfile { RamType = "ddr4" };
        var suggested = new HardwareProfile { RamType = "DDR4" };
        var suggestion = new HardwareProfileSuggestion(suggested, "raw", true);

        var vm = new ProfileDiffViewModel(current, suggestion);

        var ramTypeField = vm.Fields.First(f => f.DisplayName == "RAM loại");
        Assert.False(ramTypeField.Changed);
    }

    [Fact]
    public void CanApply_false_when_parse_failed()
    {
        var suggestion = new HardwareProfileSuggestion(new HardwareProfile(), "raw", false);

        var vm = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.False(vm.CanApply);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void CanApply_true_when_parse_succeeded()
    {
        var suggestion = new HardwareProfileSuggestion(new HardwareProfile(), "raw", true);

        var vm = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.True(vm.CanApply);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void RawResponse_and_SuggestedProfile_are_exposed_verbatim()
    {
        var suggestedProfile = new HardwareProfile { CpuTdpW = 42 };
        var suggestion = new HardwareProfileSuggestion(suggestedProfile, "raw response text", true);

        var vm = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.Equal("raw response text", vm.RawResponse);
        Assert.Equal(42, vm.SuggestedProfile.CpuTdpW);
    }

    [Fact]
    public void Constructor_throws_on_null_current_profile()
    {
        var suggestion = new HardwareProfileSuggestion(new HardwareProfile(), "raw", true);

        Assert.Throws<ArgumentNullException>(() => new ProfileDiffViewModel(null!, suggestion));
    }

    [Fact]
    public void Constructor_throws_on_null_suggestion()
    {
        Assert.Throws<ArgumentNullException>(() => new ProfileDiffViewModel(new HardwareProfile(), null!));
    }

    [Fact]
    public void Psu_rating_diff_uses_enum_string_comparison()
    {
        var current = new HardwareProfile { PsuRating = PsuRating.Bronze };
        var suggested = new HardwareProfile { PsuRating = PsuRating.Gold };
        var suggestion = new HardwareProfileSuggestion(suggested, "raw", true);

        var vm = new ProfileDiffViewModel(current, suggestion);

        var psuRatingField = vm.Fields.First(f => f.DisplayName == "PSU Rating");
        Assert.True(psuRatingField.Changed);
        Assert.Equal("Bronze", psuRatingField.CurrentValue);
        Assert.Equal("Gold", psuRatingField.SuggestedValue);
    }

    [Fact]
    public void All_seventeen_profile_fields_are_present_in_diff()
    {
        var suggestion = new HardwareProfileSuggestion(new HardwareProfile(), "raw", true);
        var vm = new ProfileDiffViewModel(new HardwareProfile(), suggestion);

        Assert.Equal(17, vm.Fields.Count);
    }
}
