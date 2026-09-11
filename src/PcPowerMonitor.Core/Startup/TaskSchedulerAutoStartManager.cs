using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Startup;

/// <summary>
/// <see cref="IAutoStartManager"/> backed by <c>schtasks.exe</c> (no extra NuGet).
/// Uses the full <c>%SystemRoot%\System32\schtasks.exe</c> path to defeat PATH
/// hijacking, runs the child with stderr captured and a 10s watchdog, and turns a
/// non-zero exit into an <see cref="AutoStartException"/> with a Vietnamese message.
/// </summary>
public sealed class TaskSchedulerAutoStartManager : IAutoStartManager
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<TaskSchedulerAutoStartManager>? _log;
    private readonly string _schtasksPath;
    private readonly string _exePath;

    public TaskSchedulerAutoStartManager(ILogger<TaskSchedulerAutoStartManager>? log = null)
    {
        _log = log;
        _schtasksPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        _exePath = Environment.ProcessPath
            ?? throw new AutoStartException("Không xác định được đường dẫn tiến trình hiện tại.");
    }

    public bool IsEnabled()
        => Run(TaskSchedulerArgumentBuilder.BuildQueryArguments()).ExitCode == 0;

    public void Enable()
    {
        var result = Run(TaskSchedulerArgumentBuilder.BuildCreateArguments(_exePath));
        if (result.ExitCode != 0)
            throw new AutoStartException(Describe("Không thể bật khởi động cùng Windows", result));
        _log?.LogInformation("Đã tạo scheduled task khởi động cùng Windows.");
    }

    public void Disable()
    {
        var result = Run(TaskSchedulerArgumentBuilder.BuildDeleteArguments());
        if (result.ExitCode != 0 && !LooksMissing(result.StdErr))
            throw new AutoStartException(Describe("Không thể tắt khởi động cùng Windows", result));
        _log?.LogInformation("Đã xóa scheduled task khởi động cùng Windows.");
    }

    private ProcessResult Run(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _schtasksPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)
                ?? throw new AutoStartException("Không khởi chạy được schtasks.exe.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
            {
                TryKill(process);
                throw new AutoStartException("schtasks.exe không phản hồi sau 10 giây.");
            }

            return new ProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (AutoStartException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AutoStartException($"Gọi schtasks.exe thất bại: {ex.Message}", ex);
        }
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }

    private static bool LooksMissing(string stderr)
        => stderr.Contains("cannot find", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase);

    private static string Describe(string prefix, ProcessResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StdErr)
            ? $"mã lỗi {result.ExitCode}"
            : result.StdErr.Trim();
        return $"{prefix}: {detail}. Bạn có thể cần chạy bằng quyền Administrator, " +
               "hoặc chính sách nhóm (Group Policy) đang chặn Task Scheduler.";
    }

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr);
}
