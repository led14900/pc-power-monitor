using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Security;

namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Pure clamp of an untrusted (hand-edited) <see cref="AppSettings"/> into safe
/// ranges. Returns the corrected copy plus a list of human warnings for logging.
/// Ranges: interval 1-60s, retention 1-365d, CPU/GPU temp 40-110°C, power 50-2000W,
/// cooldown 1-240min. Hardware profile delegates to <c>HardwareProfile.Clamped()</c>;
/// tariff price outside 0-100000 falls back to 3460, VAT is clamped to 0-0.5.
/// </summary>
public static class SettingsValidator
{
    private const double DefaultUnitPriceVnd = 3460d;

    public static (AppSettings Settings, IReadOnlyList<string> Warnings) Clamp(AppSettings input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var w = new List<string>();

        var interval = ClampInt(input.Sampling.IntervalSeconds, 1, 60, "khoảng lấy mẫu (giây)", w);
        var retention = ClampInt(input.Storage.RetentionDays, 1, 365, "số ngày lưu trữ", w);
        var cpu = ClampDouble(input.Alerts.CpuTempC, 40, 110, "ngưỡng nhiệt CPU", w);
        var gpu = ClampDouble(input.Alerts.GpuTempC, 40, 110, "ngưỡng nhiệt GPU", w);
        var power = ClampDouble(input.Alerts.PowerW, 50, 2000, "ngưỡng công suất", w);
        var cooldown = ClampInt(input.Alerts.CooldownMinutes, 1, 240, "thời gian chờ cảnh báo", w);

        var profile = input.HardwareProfile.Clamped();
        if (!profile.Equals(input.HardwareProfile))
            w.Add("Hồ sơ phần cứng có giá trị ngoài khoảng hợp lệ, đã tự điều chỉnh.");

        var tariff = ClampTariff(input.Tariff, w);
        var ai = ClampAi(input.Ai, w);

        var settings = input with
        {
            Sampling = input.Sampling with { IntervalSeconds = interval },
            Storage = input.Storage with { RetentionDays = retention },
            Alerts = input.Alerts with
            {
                CpuTempC = cpu,
                GpuTempC = gpu,
                PowerW = power,
                CooldownMinutes = cooldown,
            },
            HardwareProfile = profile,
            Tariff = tariff,
            Ai = ai,
        };
        return (settings, w);
    }

    /// <summary>
    /// Validates Vertex AI-specific fields: service account JSON must decrypt (if
    /// present — the feature is opt-in), Project Id must be present when a service
    /// account is configured, Region falls back to a default. Warns (without failing)
    /// on undecryptable blobs — e.g. settings.json was copied from another machine/user
    /// — the feature's own UI treats a non-decrypting secret as "not configured"; this
    /// only surfaces the warning.
    /// </summary>
    private static AiSettings ClampAi(AiSettings ai, List<string> w)
    {
        if (string.IsNullOrEmpty(ai.EncryptedServiceAccountJson))
        {
            var modelIdOnly = ClampModelId(ai.ModelId, w);
            var regionOnly = string.IsNullOrWhiteSpace(ai.Region) ? "us-central1" : ai.Region;
            return ai with { ModelId = modelIdOnly, Region = regionOnly };
        }

        if (!DpapiKeyProtector.TryDecrypt(ai.EncryptedServiceAccountJson, out _))
            w.Add("Service account JSON không giải mã được (có thể do sao chép từ máy/tài khoản khác) — cần chọn lại file.");

        if (string.IsNullOrWhiteSpace(ai.ProjectId))
            w.Add("Vertex AI cần Project ID.");

        var modelId = ClampModelId(ai.ModelId, w);
        var region = string.IsNullOrWhiteSpace(ai.Region) ? "us-central1" : ai.Region;

        return ai with { ModelId = modelId, Region = region };
    }

    private static string ClampModelId(string modelId, List<string> w)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
            return modelId;

        w.Add("Model AI trống; dùng mặc định \"gemini-3.5-flash-lite\".");
        return "gemini-3.5-flash-lite";
    }

    private static TariffSettings ClampTariff(TariffSettings tariff, List<string> w)
    {
        var price = tariff.UnitPriceVnd;
        if (!double.IsFinite(price) || price < 0d || price > 100_000d)
        {
            w.Add($"Đơn giá điện không hợp lệ ({price}); dùng mặc định {DefaultUnitPriceVnd:0} đ/kWh.");
            price = DefaultUnitPriceVnd;
        }

        var vat = tariff.VatRate;
        if (!double.IsFinite(vat) || vat < 0d || vat > 0.5d)
        {
            var clamped = double.IsFinite(vat) ? Math.Clamp(vat, 0d, 0.5d) : 0d;
            w.Add($"Thuế VAT ngoài khoảng 0-50% ({vat}); điều chỉnh về {clamped:P0}.");
            vat = clamped;
        }

        return tariff with { UnitPriceVnd = price, VatRate = vat };
    }

    private static int ClampInt(int value, int lo, int hi, string label, List<string> w)
    {
        var clamped = Math.Clamp(value, lo, hi);
        if (clamped != value)
            w.Add($"Giá trị {label} ({value}) ngoài khoảng {lo}-{hi}; điều chỉnh về {clamped}.");
        return clamped;
    }

    private static double ClampDouble(double value, double lo, double hi, string label, List<string> w)
    {
        var clamped = double.IsFinite(value) ? Math.Clamp(value, lo, hi) : lo;
        if (Math.Abs(clamped - value) > 1e-9 || !double.IsFinite(value))
            w.Add($"Giá trị {label} ({value}) ngoài khoảng {lo}-{hi}; điều chỉnh về {clamped}.");
        return clamped;
    }
}
