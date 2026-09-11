using CommunityToolkit.Mvvm.ComponentModel;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// One row in the "Phần cứng" tab. Identity (<see cref="Group"/> + <see cref="Name"/>
/// + <see cref="Unit"/>) is fixed for the life of the row; only <see cref="Value"/>
/// changes on each refresh so the bound <c>ObservableCollection</c> is never rebuilt.
/// </summary>
public sealed partial class SensorItemViewModel : ObservableObject
{
    public SensorItemViewModel(string group, string name, string unit, double? value)
    {
        Group = group;
        Name = name;
        Unit = unit;
        this.value = value;
    }

    public string Group { get; }

    public string Name { get; }

    public string Unit { get; }

    /// <summary>Latest reading, or null when the sensor reported nothing (renders "—").</summary>
    [ObservableProperty]
    private double? value;
}
