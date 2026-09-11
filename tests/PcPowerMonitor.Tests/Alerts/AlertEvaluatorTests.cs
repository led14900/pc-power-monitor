using PcPowerMonitor.Core.Alerts;

namespace PcPowerMonitor.Tests.Alerts;

public sealed class AlertEvaluatorTests
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Null_value_never_fires()
    {
        var state = new AlertState();
        Assert.False(AlertEvaluator.ShouldFire(null, 80, state, T0, Cooldown));
    }

    [Fact]
    public void Value_below_threshold_never_fires()
    {
        var state = new AlertState();
        Assert.False(AlertEvaluator.ShouldFire(50, 80, state, T0, Cooldown));
    }

    [Fact]
    public void Crossing_threshold_while_armed_fires_and_disarms()
    {
        var state = new AlertState();

        Assert.True(AlertEvaluator.ShouldFire(90, 80, state, T0, Cooldown));
        Assert.False(state.Armed);
        Assert.Equal(T0, state.LastFiredUtc);
    }

    [Fact]
    public void Does_not_refire_within_cooldown()
    {
        var state = new AlertState();
        AlertEvaluator.ShouldFire(90, 80, state, T0, Cooldown);

        Assert.False(AlertEvaluator.ShouldFire(90, 80, state, T0.AddSeconds(1), Cooldown));
    }

    [Fact]
    public void Does_not_refire_after_cooldown_if_not_rearmed()
    {
        var state = new AlertState();
        AlertEvaluator.ShouldFire(90, 80, state, T0, Cooldown);

        Assert.False(AlertEvaluator.ShouldFire(90, 80, state, T0.AddMinutes(11), Cooldown));
    }

    [Fact]
    public void Falling_below_hysteresis_band_rearms()
    {
        var state = new AlertState();
        AlertEvaluator.ShouldFire(90, 80, state, T0, Cooldown);

        Assert.False(AlertEvaluator.ShouldFire(74, 80, state, T0.AddMinutes(12), Cooldown)); // 74 < 80 - 5
        Assert.True(state.Armed);
    }

    [Fact]
    public void Refires_once_rearmed_and_past_cooldown()
    {
        var state = new AlertState();
        AlertEvaluator.ShouldFire(90, 80, state, T0, Cooldown);
        AlertEvaluator.ShouldFire(74, 80, state, T0.AddMinutes(12), Cooldown); // re-arm

        Assert.True(AlertEvaluator.ShouldFire(90, 80, state, T0.AddMinutes(25), Cooldown));
    }
}
