using PcPowerMonitor.Core.Startup;

namespace PcPowerMonitor.Tests.Startup;

public sealed class TaskSchedulerArgumentBuilderTests
{
    [Fact]
    public void BuildCreateArguments_quotes_a_path_containing_spaces()
    {
        var args = TaskSchedulerArgumentBuilder.BuildCreateArguments(@"G:\My Drive\app.exe");

        // schtasks /TR wants the run target as one quoted token with the exe path
        // re-quoted via \" so the space in "My Drive" does not split the argument.
        Assert.Contains(@"/TR ""\""G:\My Drive\app.exe\"" --autostart""", args);
        Assert.Contains(@"/TN ""PcPowerMonitorAutoStart""", args);
        Assert.Contains("/SC ONLOGON", args);
        Assert.Contains("/RL HIGHEST", args);
        Assert.Contains("/IT", args);
        Assert.Contains("/F", args);
    }

    [Fact]
    public void BuildCreateArguments_still_quotes_a_path_without_spaces()
    {
        var args = TaskSchedulerArgumentBuilder.BuildCreateArguments(@"C:\Tools\app.exe");

        Assert.Contains(@"\""C:\Tools\app.exe\"" --autostart", args);
        Assert.StartsWith("/Create ", args);
    }

    [Fact]
    public void BuildCreateArguments_trims_surrounding_whitespace_in_path()
    {
        var args = TaskSchedulerArgumentBuilder.BuildCreateArguments("  C:\\app.exe  ");

        Assert.Contains(@"\""C:\app.exe\"" --autostart", args);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildCreateArguments_rejects_blank_path(string? path)
        => Assert.Throws<ArgumentException>(() => TaskSchedulerArgumentBuilder.BuildCreateArguments(path!));

    [Fact]
    public void Delete_and_query_target_the_named_task()
    {
        Assert.Equal(@"/Delete /TN ""PcPowerMonitorAutoStart"" /F",
            TaskSchedulerArgumentBuilder.BuildDeleteArguments());
        Assert.Equal(@"/Query /TN ""PcPowerMonitorAutoStart""",
            TaskSchedulerArgumentBuilder.BuildQueryArguments());
    }
}
