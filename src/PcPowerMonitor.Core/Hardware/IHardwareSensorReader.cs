using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Abstraction over the hardware sensor source. Implementations must never throw
/// from <see cref="ReadSnapshot"/> — on failure they return an all-null snapshot
/// carrying the current <see cref="Availability"/>.
/// </summary>
public interface IHardwareSensorReader : IDisposable
{
    /// <summary>Full / Partial / Unavailable, decided once at construction.</summary>
    SensorAvailability Availability { get; }

    /// <summary>Human-readable Vietnamese reason for a non-Full state, for the UI.</summary>
    string? AvailabilityReason { get; }

    /// <summary>Reads one point-in-time snapshot of every enabled hardware group.</summary>
    HardwareSnapshot ReadSnapshot();

    /// <summary>
    /// Flat dump of every discovered hardware + sensor (serials scrubbed).
    /// Written to the log once at startup to debug a user's machine remotely.
    /// </summary>
    IReadOnlyList<string> GetDiagnostics();
}
