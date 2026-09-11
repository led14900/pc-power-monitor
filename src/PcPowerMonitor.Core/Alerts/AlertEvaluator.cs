namespace PcPowerMonitor.Core.Alerts;

/// <summary>
/// Pure decision function for one alert channel. Takes the clock as a parameter so it
/// is fully testable. Mutates the passed <see cref="AlertState"/>: clears
/// <see cref="AlertState.Armed"/> and stamps <see cref="AlertState.LastFiredUtc"/> when
/// it returns true; re-arms once the value falls below <c>threshold - hysteresis</c>.
/// </summary>
public static class AlertEvaluator
{
    public static bool ShouldFire(
        double? value,
        double threshold,
        AlertState state,
        DateTimeOffset nowUtc,
        TimeSpan cooldown,
        double hysteresis = 5)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (value is not { } v || !double.IsFinite(v)) return false;
        if (v < threshold - hysteresis) { state.Armed = true; return false; }
        if (v <= threshold) return false;
        if (!state.Armed) return false;
        if (state.LastFiredUtc is { } last && nowUtc - last < cooldown) return false;

        state.Armed = false;
        state.LastFiredUtc = nowUtc;
        return true;
    }
}
