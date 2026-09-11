using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class EnergyAccumulatorTests
{
    private static (EnergyAccumulator Acc, FakeElapsedClock Clock) NewAccumulator(double intervalSeconds = 2d)
    {
        var clock = new FakeElapsedClock();
        return (new EnergyAccumulator(clock, logger: null, intervalSeconds), clock);
    }

    [Fact]
    public void First_add_only_seeds_and_contributes_no_energy()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;

        var inc = acc.Add(200d);

        Assert.Equal(0d, inc.Kwh);
        Assert.False(inc.GapDetected);
        Assert.Equal(0d, acc.TotalKwh);
    }

    [Fact]
    public void Trapezoidal_integration_1800_steps_of_2s_at_200w_is_0_2_kwh()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;
        acc.Add(200d); // seed

        for (var i = 0; i < 1800; i++)
        {
            clock.ElapsedSeconds = 2d;
            acc.Add(200d);
        }

        Assert.Equal(0.2d, acc.TotalKwh, 6);
        Assert.True(Math.Abs(acc.TotalKwh - 0.2d) / 0.2d < 0.001d);
    }

    [Fact]
    public void Canary_200w_for_one_hour_is_0_2_kwh_single_step()
    {
        var (acc, clock) = NewAccumulator(intervalSeconds: 3600d);
        clock.ElapsedSeconds = 1d;
        acc.Add(200d); // seed
        clock.ElapsedSeconds = 3600d;

        var inc = acc.Add(200d);

        Assert.Equal(0.2d, inc.Kwh, 9);
        Assert.Equal(0.2d, acc.TotalKwh, 9);
    }

    [Fact]
    public void Sleep_8h_gap_is_detected_and_adds_no_energy()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;
        acc.Add(200d); // seed
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        var before = acc.TotalKwh;

        clock.ElapsedSeconds = 28_800d; // 8 hours
        var inc = acc.Add(200d);

        Assert.True(inc.GapDetected);
        Assert.Equal(0d, inc.Kwh);
        Assert.Equal(28_800d, inc.DtSeconds);
        Assert.Equal(before, acc.TotalKwh);
    }

    [Fact]
    public void Non_finite_wall_power_is_ignored_and_state_stays_valid()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        var stable = acc.TotalKwh;

        clock.ElapsedSeconds = 2d;
        var nanInc = acc.Add(double.NaN);
        clock.ElapsedSeconds = 2d;
        var infInc = acc.Add(double.PositiveInfinity);

        Assert.Equal(0d, nanInc.Kwh);
        Assert.Equal(0d, infInc.Kwh);
        Assert.Equal(stable, acc.TotalKwh);

        // Previous good reading (200 W) is intact: the next step integrates normally.
        clock.ElapsedSeconds = 2d;
        var good = acc.Add(200d);
        Assert.True(good.Kwh > 0d);
    }

    [Fact]
    public void MarkGap_drops_reference_point_so_next_add_only_reseeds()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        var total = acc.TotalKwh;

        acc.MarkGap();

        clock.ElapsedSeconds = 2d;
        var reseed = acc.Add(200d);
        Assert.Equal(0d, reseed.Kwh);
        Assert.Equal(total, acc.TotalKwh);

        clock.ElapsedSeconds = 2d;
        var next = acc.Add(200d);
        Assert.True(next.Kwh > 0d);
    }

    [Fact]
    public void Reset_replaces_total_and_requires_fresh_seed()
    {
        var (acc, clock) = NewAccumulator();
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);
        clock.ElapsedSeconds = 2d;
        acc.Add(200d);

        acc.Reset(5.0d);
        Assert.Equal(5.0d, acc.TotalKwh);

        clock.ElapsedSeconds = 2d;
        var afterReset = acc.Add(200d);
        Assert.Equal(0d, afterReset.Kwh); // reseed, not integrate
        Assert.Equal(5.0d, acc.TotalKwh);
    }

    [Fact]
    public void Reset_rejects_negative_or_non_finite_seed()
    {
        var (acc, _) = NewAccumulator();
        Assert.Throws<ArgumentOutOfRangeException>(() => acc.Reset(-1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => acc.Reset(double.NaN));
    }

    [Fact]
    public void Gap_threshold_is_max_of_triple_interval_and_ten_seconds()
    {
        var (acc, clock) = NewAccumulator(intervalSeconds: 5d); // threshold = 15s
        clock.ElapsedSeconds = 5d;
        acc.Add(100d);

        clock.ElapsedSeconds = 14d; // under threshold → integrates
        Assert.False(acc.Add(100d).GapDetected);

        clock.ElapsedSeconds = 16d; // over threshold → gap
        Assert.True(acc.Add(100d).GapDetected);
    }

    [Fact]
    public void SetExpectedInterval_widens_gap_threshold_so_slow_sampling_still_integrates()
    {
        var (acc, clock) = NewAccumulator(intervalSeconds: 2d); // default threshold = 10s
        acc.SetExpectedInterval(30d);                           // threshold now max(90, 10) = 90s

        clock.ElapsedSeconds = 30d;
        acc.Add(200d); // seed

        clock.ElapsedSeconds = 30d;
        var inc = acc.Add(200d);

        Assert.False(inc.GapDetected);
        Assert.True(inc.Kwh > 0d);
        Assert.True(acc.TotalKwh > 0d);
    }
}
