namespace PcPowerMonitor.Core.Startup;

/// <summary>
/// Thrown when configuring Windows auto-start fails (schtasks non-zero exit, timeout,
/// blocked by group policy…). Message is user-facing Vietnamese.
/// </summary>
public sealed class AutoStartException : Exception
{
    public AutoStartException(string message) : base(message) { }

    public AutoStartException(string message, Exception innerException) : base(message, innerException) { }
}
