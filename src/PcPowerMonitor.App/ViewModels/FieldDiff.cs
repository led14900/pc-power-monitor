namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// One row of the AI lookup confirmation dialog's diff table: a single
/// <see cref="PcPowerMonitor.Core.Power.HardwareProfile"/> field compared between the
/// current (user-saved) value and the AI-suggested value.
/// </summary>
public sealed record FieldDiff(
    string DisplayName,
    string CurrentValue,
    string SuggestedValue,
    string Unit,
    bool Changed);
