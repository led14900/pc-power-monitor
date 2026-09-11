using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Infrastructure;

namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// <see cref="ISettingsStore"/> backed by an indented, camelCase <c>settings.json</c>.
/// Load: missing file → defaults written; unreadable / corrupt file → the bad copy is
/// quarantined as <c>settings.bad-{timestamp}.json</c>, defaults are used and a warning
/// logged (never throws). Save: serialise → <c>settings.json.tmp</c> →
/// <see cref="File.Replace(string,string,string)"/> onto the real file keeping a
/// <c>settings.bak</c>, then raise <see cref="Changed"/>. All writes are serialised
/// by a lock. Every value passes through <see cref="SettingsValidator"/> on both paths.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogger<JsonSettingsStore>? _log;
    private readonly string _path;
    private readonly string _tmpPath;
    private readonly string _bakPath;
    private readonly object _gate = new();

    private AppSettings _current;

    public JsonSettingsStore(ILogger<JsonSettingsStore>? log = null)
    {
        _log = log;
        _path = AppPaths.SettingsFile;
        _tmpPath = _path + ".tmp";
        _bakPath = Path.ChangeExtension(_path, ".bak");
        _current = Load();
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event Action<AppSettings>? Changed;

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var (clamped, warnings) = SettingsValidator.Clamp(settings);
        LogWarnings(warnings);

        lock (_gate)
        {
            WriteFile(clamped);
            Volatile.Write(ref _current, clamped);
        }

        Changed?.Invoke(clamped);
    }

    private AppSettings Load()
    {
        try
        {
            AppPaths.EnsureCreated();

            if (!File.Exists(_path))
            {
                var (defaults, _) = SettingsValidator.Clamp(new AppSettings());
                lock (_gate) WriteFile(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            var (clamped, warnings) = SettingsValidator.Clamp(parsed);
            LogWarnings(warnings);
            return clamped;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Quarantine(ex);
            return new AppSettings();
        }
    }

    private void WriteFile(AppSettings settings)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_tmpPath, json);

        if (File.Exists(_path))
            File.Replace(_tmpPath, _path, _bakPath);
        else
            File.Move(_tmpPath, _path);
    }

    private void Quarantine(Exception ex)
    {
        try
        {
            var bad = Path.Combine(
                AppPaths.RootDir, $"settings.bad-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(_path, bad, overwrite: true);
            _log?.LogWarning(ex, "settings.json không đọc được; đã lưu bản lỗi vào {Bad} và dùng mặc định.", bad);
        }
        catch (Exception copyEx)
        {
            _log?.LogWarning(copyEx, "settings.json không đọc được và không sao lưu được bản lỗi; dùng mặc định.");
        }
    }

    private void LogWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
            _log?.LogWarning("Cài đặt: {Warning}", warning);
    }
}
