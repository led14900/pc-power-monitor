using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Tests.Settings;

public sealed class SettingsValidatorTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(9999, 60)]
    [InlineData(2, 2)]
    public void Interval_is_clamped_to_1_to_60(int input, int expected)
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Sampling = new SamplingSettings { IntervalSeconds = input } });

        Assert.Equal(expected, result.Sampling.IntervalSeconds);
        if (input != expected) Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Retention_above_365_is_clamped()
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Storage = new StorageSettings { RetentionDays = 500 } });

        Assert.Equal(365, result.Storage.RetentionDays);
        Assert.NotEmpty(warnings);
    }

    [Theory]
    [InlineData(5, 40)]
    [InlineData(200, 110)]
    public void Temp_thresholds_are_clamped_to_40_to_110(double input, double expected)
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Alerts = new AlertSettings { CpuTempC = input, GpuTempC = input } });

        Assert.Equal(expected, result.Alerts.CpuTempC);
        Assert.Equal(expected, result.Alerts.GpuTempC);
        Assert.NotEmpty(warnings);
    }

    [Theory]
    [InlineData(10, 50)]
    [InlineData(5000, 2000)]
    public void Power_threshold_is_clamped_to_50_to_2000(double input, double expected)
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Alerts = new AlertSettings { PowerW = input } });

        Assert.Equal(expected, result.Alerts.PowerW);
        Assert.NotEmpty(warnings);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-5d)]
    [InlineData(999_999d)]
    public void Garbage_tariff_price_falls_back_to_3460(double price)
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Tariff = new TariffSettings { UnitPriceVnd = price } });

        Assert.Equal(3460d, result.Tariff.UnitPriceVnd);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Vat_above_half_is_clamped_to_0_5()
    {
        var (result, warnings) = SettingsValidator.Clamp(
            new AppSettings { Tariff = new TariffSettings { VatRate = 0.9d } });

        Assert.Equal(0.5d, result.Tariff.VatRate);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Valid_settings_produce_no_warnings()
    {
        // Use a clamped profile (all fields set, no sentinels) to avoid warnings
        var profile = new AppSettings().HardwareProfile.Clamped();
        var settings = new AppSettings { HardwareProfile = profile };

        var (_, warnings) = SettingsValidator.Clamp(settings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Ai_settings_not_configured_produces_no_warning()
    {
        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = null, ModelId = "gemini-pro" }
        };

        var (result, warnings) = SettingsValidator.Clamp(settings);

        Assert.Null(result.Ai.EncryptedServiceAccountJson);
        Assert.Equal("gemini-pro", result.Ai.ModelId);
        // No warnings for unconfigured AI
        var aiWarnings = warnings.Where(w => w.Contains("AI") || w.Contains("Model") || w.Contains("Vertex")).ToList();
        Assert.Empty(aiWarnings);
    }

    [Fact]
    public void Ai_settings_blank_model_id_defaults_to_gemini_3_5_flash_lite()
    {
        // Encrypt a fake service-account blob first
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = encryptedServiceAccount, ProjectId = "proj", ModelId = "" }
        };

        var (result, warnings) = SettingsValidator.Clamp(settings);

        Assert.Equal("gemini-3.5-flash-lite", result.Ai.ModelId);
        var modelWarnings = warnings.Where(w => w.Contains("Model") || w.Contains("trống")).ToList();
        Assert.NotEmpty(modelWarnings);
    }

    [Fact]
    public void Ai_settings_whitespace_model_id_defaults_to_gemini_3_5_flash_lite()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = encryptedServiceAccount, ProjectId = "proj", ModelId = "   " }
        };

        var (result, warnings) = SettingsValidator.Clamp(settings);

        Assert.Equal("gemini-3.5-flash-lite", result.Ai.ModelId);
        var modelWarnings = warnings.Where(w => w.Contains("Model") || w.Contains("trống")).ToList();
        Assert.NotEmpty(modelWarnings);
    }

    [Fact]
    public void Ai_settings_corrupt_service_account_produces_warning()
    {
        // Use a deliberately corrupt/untamper-able base64 blob
        var corruptBase64 = "AAAA"; // Too short to be a real DPAPI blob

        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = corruptBase64, ProjectId = "proj", ModelId = "gemini-pro" }
        };

        var (result, warnings) = SettingsValidator.Clamp(settings);

        // Settings should be preserved
        Assert.Equal(corruptBase64, result.Ai.EncryptedServiceAccountJson);
        Assert.Equal("gemini-pro", result.Ai.ModelId);

        // But should have a warning about decryption failure
        var decryptWarnings = warnings.Where(w => w.Contains("không giải mã") || w.Contains("decryp")).ToList();
        Assert.NotEmpty(decryptWarnings);
    }

    [Fact]
    public void Ai_settings_valid_service_account_no_decrypt_warning()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = encryptedServiceAccount, ProjectId = "proj", ModelId = "gemini-pro" }
        };

        var (result, warnings) = SettingsValidator.Clamp(settings);

        Assert.Equal(encryptedServiceAccount, result.Ai.EncryptedServiceAccountJson);
        Assert.Equal("gemini-pro", result.Ai.ModelId);

        var aiWarnings = warnings.Where(w => w.Contains("không giải mã") || w.Contains("decryp")).ToList();
        Assert.Empty(aiWarnings);
    }

    [Fact]
    public void Ai_settings_missing_project_id_warns()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings { EncryptedServiceAccountJson = encryptedServiceAccount, ProjectId = null, ModelId = "gemini-pro" }
        };

        var (_, warnings) = SettingsValidator.Clamp(settings);

        Assert.Contains(warnings, w => w.Contains("Project ID"));
    }

    [Fact]
    public void Ai_device_type_desktop_preserved()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                EncryptedServiceAccountJson = encryptedServiceAccount,
                ProjectId = "proj",
                ModelId = "gemini-pro",
                DeviceType = AiDeviceType.Desktop
            }
        };

        var (result, _) = SettingsValidator.Clamp(settings);

        Assert.Equal(AiDeviceType.Desktop, result.Ai.DeviceType);
    }

    [Fact]
    public void Ai_device_type_laptop_preserved()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                EncryptedServiceAccountJson = encryptedServiceAccount,
                ProjectId = "proj",
                ModelId = "gemini-pro",
                DeviceType = AiDeviceType.Laptop
            }
        };

        var (result, _) = SettingsValidator.Clamp(settings);

        Assert.Equal(AiDeviceType.Laptop, result.Ai.DeviceType);
    }

    [Fact]
    public void Ai_machine_model_preserved()
    {
        var encryptedServiceAccount = PcPowerMonitor.Core.Security.DpapiKeyProtector.Encrypt("test-fake-sa-123");

        var settings = new AppSettings
        {
            Ai = new AiSettings
            {
                EncryptedServiceAccountJson = encryptedServiceAccount,
                ProjectId = "proj",
                ModelId = "gemini-pro",
                MachineModel = "Lenovo ThinkCentre M700"
            }
        };

        var (result, _) = SettingsValidator.Clamp(settings);

        Assert.Equal("Lenovo ThinkCentre M700", result.Ai.MachineModel);
    }
}
