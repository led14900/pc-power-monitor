using LibreHardwareMonitor.Hardware;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Visitor that refreshes the entire hardware tree in one pass. Recurses into
/// <see cref="IHardware.SubHardware"/> so GPUs hanging off the mainboard and NVMe
/// drives behind a storage controller are not missed.
/// </summary>
internal sealed class LhmUpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            sub.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
