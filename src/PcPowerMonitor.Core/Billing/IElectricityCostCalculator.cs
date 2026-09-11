namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// Pure EVN cost maths: kWh + a tariff → a <see cref="CostBreakdown"/>. No I/O, no clock,
/// no rounding. <see cref="ElectricityCostCalculator"/> is the sole implementation and it
/// also adapts to <see cref="PcPowerMonitor.Core.Storage.ICostCalculator"/> for the rollup
/// pipeline.
/// </summary>
public interface IElectricityCostCalculator
{
    CostBreakdown Calculate(double kwh, TariffSettings tariff);
}
