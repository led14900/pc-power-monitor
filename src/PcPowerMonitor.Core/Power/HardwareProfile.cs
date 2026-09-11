namespace PcPowerMonitor.Core.Power;

/// <summary>
/// User-supplied description of the machine: component TDPs, part counts and PSU
/// spec. Serialized to JSON in phase 08. Every value has a sane default so a fresh
/// install produces a usable (if rough) estimate. There is deliberately NO monitor
/// field — v1 bills only what flows through the PC's PSU (Validation Session 1).
/// </summary>
public sealed record HardwareProfile
{
    /// <summary>CPU thermal design power in watts. Used only when the CPU has no power sensor.</summary>
    public double CpuTdpW { get; init; } = 65;

    /// <summary>Discrete GPU TDP in watts. 0 means "no discrete GPU".</summary>
    public double GpuTdpW { get; init; } = 0;

    public int RamSticks { get; init; } = 2;

    /// <summary>"DDR4" or "DDR5" (case-insensitive); anything else is treated as DDR4.</summary>
    public string RamType { get; init; } = "DDR4";

    public int SsdCount { get; init; } = 1;
    public int HddCount { get; init; } = 0;
    public int FanCount { get; init; } = 3;

    /// <summary>Motherboard wattage at active/peak CPU load (used as the "active" end of load interpolation).</summary>
    public double MotherboardW { get; init; } = 60;
    public double PeripheralW { get; init; } = 3;

    /// <summary>Per-SSD idle wattage. &lt;= 0 = auto-derive as <see cref="SsdActiveW"/> x 0.25.</summary>
    public double SsdIdleW { get; init; } = -1;

    /// <summary>Per-SSD active wattage (during I/O). Interpolated against per-device activity% from LHM.</summary>
    public double SsdActiveW { get; init; } = 3.5;

    /// <summary>Motherboard idle wattage. &lt;= 0 = auto-derive as <see cref="MotherboardW"/> x 0.65.</summary>
    public double MotherboardIdleW { get; init; } = -1;

    /// <summary>Per-fan idle wattage (at minimum RPM).</summary>
    public double FanIdleW { get; init; } = 0.5;

    /// <summary>Per-fan active wattage (at max RPM). Power scales with RPM cubed (affinity laws).</summary>
    public double FanActiveW { get; init; } = 3.0;

    /// <summary>Expected max fan RPM. Used to normalize an RPM sensor reading to a 0-1 fraction.</summary>
    public double FanMaxRpm { get; init; } = 2000;

    public int PsuWattage { get; init; } = 650;
    public PsuRating PsuRating { get; init; } = PsuRating.Bronze;

    /// <summary>
    /// Returns a copy with every field forced into a safe range. JSON is user-editable
    /// so values can be garbage; clamp instead of throwing. TDP 1-1000W, counts 0-32,
    /// board/peripheral 0-1000W, PSU 100-2000W (<=0 becomes the 650W default).
    /// </summary>
    public HardwareProfile Clamped()
    {
        // Active-end values are clamped first so idle sentinels (<=0) can derive a
        // proportional idle wattage from the already-sane active/peak value.
        var motherboardW = Clamp(MotherboardW, 0d, 1000d);
        var ssdActiveW = Clamp(SsdActiveW, 0.5d, 15d);

        return this with
        {
            CpuTdpW = Clamp(CpuTdpW, 1d, 1000d),
            GpuTdpW = Clamp(GpuTdpW, 0d, 1000d),
            RamSticks = Math.Clamp(RamSticks, 0, 32),
            RamType = string.Equals(RamType, "DDR5", StringComparison.OrdinalIgnoreCase) ? "DDR5" : "DDR4",
            SsdCount = Math.Clamp(SsdCount, 0, 32),
            HddCount = Math.Clamp(HddCount, 0, 32),
            FanCount = Math.Clamp(FanCount, 0, 32),
            MotherboardW = motherboardW,
            PeripheralW = Clamp(PeripheralW, 0d, 1000d),
            PsuWattage = PsuWattage <= 0 ? 650 : Math.Clamp(PsuWattage, 100, 2000),
            SsdIdleW = SsdIdleW <= 0 ? ssdActiveW * 0.25 : Clamp(SsdIdleW, 0.1d, 10d),
            SsdActiveW = ssdActiveW,
            MotherboardIdleW = MotherboardIdleW <= 0 ? motherboardW * 0.65 : Clamp(MotherboardIdleW, 1d, 500d),
            FanIdleW = Clamp(FanIdleW, 0.1d, 5d),
            FanActiveW = Clamp(FanActiveW, 0.5d, 10d),
            FanMaxRpm = Clamp(FanMaxRpm, 500d, 10000d),
        };
    }

    private static double Clamp(double value, double lo, double hi)
        => !double.IsFinite(value) ? lo : Math.Clamp(value, lo, hi);
}
