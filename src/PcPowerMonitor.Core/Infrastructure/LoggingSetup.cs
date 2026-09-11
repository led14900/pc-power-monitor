using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace PcPowerMonitor.Core.Infrastructure;

/// <summary>
/// Wires Serilog file logging into the Generic Host. Rolling by day, 7 files kept.
/// Exceptions at the driver / DB boundary must never be swallowed silently, so a
/// file sink is mandatory.
/// </summary>
public static class LoggingSetup
{
    public static void AddAppFileLogging(this IHostApplicationBuilder builder)
    {
        AppPaths.EnsureCreated();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(AppPaths.LogsDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Logger = logger;

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger, dispose: true);
    }
}
