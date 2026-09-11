using PcPowerMonitor.Core.Sampling;

namespace PcPowerMonitor.Tests.Sampling;

public sealed class SamplingOptionsTests
{
    [Fact]
    public void Default_interval_is_two_seconds_and_not_paused()
    {
        var options = new SamplingOptions();

        Assert.Equal(2, options.IntervalSeconds);
        Assert.False(options.Paused);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Interval);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(120, 60)]
    public void IntervalSeconds_is_clamped_to_the_allowed_range(int input, int expected)
    {
        var options = new SamplingOptions { IntervalSeconds = input };

        Assert.Equal(expected, options.IntervalSeconds);
        Assert.Equal(TimeSpan.FromSeconds(expected), options.Interval);
    }

    [Fact]
    public void Paused_round_trips()
    {
        var options = new SamplingOptions { Paused = true };
        Assert.True(options.Paused);

        options.Paused = false;
        Assert.False(options.Paused);
    }
}
