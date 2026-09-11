using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Integrates wall power (W) into cumulative energy (kWh) using the trapezoidal rule
/// over a monotonic <see cref="IElapsedClock"/>. Registered as a singleton; a single
/// sampling loop calls <see cref="Add"/>, but a lock guards against races anyway.
///
/// Gap handling: if the measured delta exceeds <c>max(interval x 3, 10s)</c> the step
/// is dropped (machine slept / process was paused) rather than billing the last
/// known power for the whole gap.
/// </summary>
public sealed class EnergyAccumulator
{
    /// <summary>W * s / this = kWh. (/3600 → Wh, /1000 → kWh.) Canary: 200 W for 1 h = 0.2 kWh.</summary>
    public const double WattSecondsPerKwh = 3_600_000d;

    private readonly IElapsedClock _clock;
    private readonly ILogger<EnergyAccumulator>? _log;
    private readonly object _lock = new();

    private double _gapThresholdSeconds;

    private double _previousW;
    private bool _hasPrevious;

    /// <summary>Running total. Seed it from the DB on restart via <see cref="Reset"/>.</summary>
    public double TotalKwh { get; private set; }

    public EnergyAccumulator(
        IElapsedClock clock,
        ILogger<EnergyAccumulator>? logger = null,
        double intervalSeconds = 2d)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = logger;
        _gapThresholdSeconds = Math.Max(intervalSeconds * 3d, 10d);
        _clock.Restart();
    }

    /// <summary>
    /// Feed the latest wall-power reading. Returns what this step contributed.
    /// Non-finite <paramref name="wallW"/> is ignored (defensive against NaN/Inf).
    /// </summary>
    public EnergyIncrement Add(double wallW)
    {
        lock (_lock)
        {
            var dt = _clock.ElapsedSeconds;
            _clock.Restart();

            if (!double.IsFinite(wallW))
            {
                _log?.LogWarning("Ignoring non-finite wall power {Value} in energy accumulator.", wallW);
                return EnergyIncrement.Zero(dt);
            }

            if (!_hasPrevious)
            {
                _previousW = wallW;
                _hasPrevious = true;
                return EnergyIncrement.Zero(dt);
            }

            if (dt <= 0d || dt > _gapThresholdSeconds)
            {
                _previousW = wallW;
                return EnergyIncrement.Gap(dt);
            }

            var kwh = (_previousW + wallW) / 2d * dt / WattSecondsPerKwh;
            _previousW = wallW;
            TotalKwh += kwh;
            return new EnergyIncrement(kwh, dt, false);
        }
    }

    /// <summary>
    /// Track the live sampling interval so the gap threshold stays <c>max(interval x 3, 10s)</c>.
    /// Must be called whenever the configured interval changes: without it a rate slower
    /// than ~3s makes every normal step look like a gap and no energy ever accumulates.
    /// Non-finite / non-positive values are ignored.
    /// </summary>
    public void SetExpectedInterval(double intervalSeconds)
    {
        if (!double.IsFinite(intervalSeconds) || intervalSeconds <= 0d)
            return;

        lock (_lock)
        {
            _gapThresholdSeconds = Math.Max(intervalSeconds * 3d, 10d);
        }
    }

    /// <summary>
    /// Drop the current reference point without adding energy. Call on resume from
    /// sleep or right after startup so the next <see cref="Add"/> only re-seeds.
    /// </summary>
    public void MarkGap()
    {
        lock (_lock)
        {
            _hasPrevious = false;
            _clock.Restart();
        }
    }

    /// <summary>
    /// Replace the running total (e.g. restoring from the DB). Logs old → new because
    /// this total is the user's money.
    /// </summary>
    public void Reset(double seedKwh)
    {
        if (!double.IsFinite(seedKwh) || seedKwh < 0d)
            throw new ArgumentOutOfRangeException(nameof(seedKwh), seedKwh, "Seed kWh must be finite and >= 0.");

        lock (_lock)
        {
            var old = TotalKwh;
            TotalKwh = seedKwh;
            _hasPrevious = false;
            _clock.Restart();
            _log?.LogInformation("EnergyAccumulator total reset {OldKwh} -> {NewKwh} kWh.", old, seedKwh);
        }
    }
}
