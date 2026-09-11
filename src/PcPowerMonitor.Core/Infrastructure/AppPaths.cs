namespace PcPowerMonitor.Core.Infrastructure;

/// <summary>
/// Single source of truth for every writable path the app uses.
/// Everything lives under <c>%LOCALAPPDATA%\PcPowerMonitor\</c> (never Program Files,
/// which needs admin to write, and never ProgramData, which every user could read).
/// </summary>
public static class AppPaths
{
    private static string? _rootOverride;

    /// <summary>Test hook: redirect the whole tree somewhere disposable. Pass <c>null</c> to reset.</summary>
    public static void OverrideRoot(string? root) => _rootOverride = root;

    public static string RootDir =>
        _rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PcPowerMonitor");

    public static string LogsDir => Path.Combine(RootDir, "logs");

    public static string DataDir => Path.Combine(RootDir, "data");

    public static string DatabaseFile => Path.Combine(DataDir, "monitor.db");

    public static string SettingsFile => Path.Combine(RootDir, "settings.json");

    /// <summary>
    /// Create the directory tree. Idempotent. Throws <see cref="InvalidOperationException"/>
    /// with an actionable message if the location cannot be written (policy / permissions).
    /// </summary>
    public static void EnsureCreated()
    {
        try
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(LogsDir);
            Directory.CreateDirectory(DataDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Không tạo được thư mục dữ liệu tại '{RootDir}'. " +
                "Kiểm tra quyền ghi hoặc chính sách bảo mật của máy.", ex);
        }
    }
}
