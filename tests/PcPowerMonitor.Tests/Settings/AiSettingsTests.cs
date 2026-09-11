using System.Text.Json;
using System.Text.Json.Serialization;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Tests.Settings;

// Note: Using the same serialization options as JsonSettingsStore for consistency
file static class JsonOptions
{
    public static JsonSerializerOptions Settings => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}

public sealed class AiSettingsTests
{
    [Fact]
    public void Default_model_id_is_gemini_3_5_flash_lite()
    {
        var settings = new AiSettings();
        Assert.Equal("gemini-3.5-flash-lite", settings.ModelId);
    }

    [Fact]
    public void Default_device_type_is_desktop()
    {
        var settings = new AiSettings();
        Assert.Equal(AiDeviceType.Desktop, settings.DeviceType);
    }

    [Fact]
    public void Default_region_is_us_central1()
    {
        var settings = new AiSettings();
        Assert.Equal("us-central1", settings.Region);
    }

    [Fact]
    public void All_fields_can_be_set()
    {
        var settings = new AiSettings
        {
            EncryptedServiceAccountJson = "test-encrypted-service-account",
            ProjectId = "my-gcp-project",
            Region = "asia-southeast1",
            ModelId = "gemini-4.0",
            MachineModel = "Lenovo ThinkCentre M700",
            DeviceType = AiDeviceType.Laptop,
        };

        Assert.Equal("test-encrypted-service-account", settings.EncryptedServiceAccountJson);
        Assert.Equal("my-gcp-project", settings.ProjectId);
        Assert.Equal("asia-southeast1", settings.Region);
        Assert.Equal("gemini-4.0", settings.ModelId);
        Assert.Equal("Lenovo ThinkCentre M700", settings.MachineModel);
        Assert.Equal(AiDeviceType.Laptop, settings.DeviceType);
    }

    [Fact]
    public void Json_serializes_with_defaults()
    {
        var settings = new AiSettings();
        var json = JsonSerializer.Serialize(settings, JsonOptions.Settings);

        Assert.Contains("\"modelId\"", json);
        Assert.Contains("\"gemini-3.5-flash-lite\"", json);
        Assert.Contains("\"deviceType\"", json);
        Assert.Contains("\"Desktop\"", json);
    }

    [Fact]
    public void Json_round_trip_preserves_all_fields()
    {
        var original = new AiSettings
        {
            EncryptedServiceAccountJson = "test-service-account-base64",
            ProjectId = "my-gcp-project",
            Region = "us-central1",
            ModelId = "gemini-pro",
            MachineModel = "Dell XPS 15",
            DeviceType = AiDeviceType.Laptop,
        };

        var json = JsonSerializer.Serialize(original, JsonOptions.Settings);
        var deserialized = JsonSerializer.Deserialize<AiSettings>(json, JsonOptions.Settings);

        Assert.NotNull(deserialized);
        Assert.Equal(original.EncryptedServiceAccountJson, deserialized.EncryptedServiceAccountJson);
        Assert.Equal(original.ProjectId, deserialized.ProjectId);
        Assert.Equal(original.Region, deserialized.Region);
        Assert.Equal(original.ModelId, deserialized.ModelId);
        Assert.Equal(original.MachineModel, deserialized.MachineModel);
        Assert.Equal(original.DeviceType, deserialized.DeviceType);
    }

    [Fact]
    public void Device_type_laptop_serializes_deserializes_correctly()
    {
        var settings = new AiSettings { DeviceType = AiDeviceType.Laptop };
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<AiSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(AiDeviceType.Laptop, deserialized.DeviceType);
    }

    [Fact]
    public void Device_type_desktop_serializes_deserializes_correctly()
    {
        var settings = new AiSettings { DeviceType = AiDeviceType.Desktop };
        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<AiSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(AiDeviceType.Desktop, deserialized.DeviceType);
    }

    [Fact]
    public void Json_round_trip_with_null_encrypted_service_account()
    {
        var original = new AiSettings { EncryptedServiceAccountJson = null };
        var json = JsonSerializer.Serialize(original, JsonOptions.Settings);
        var deserialized = JsonSerializer.Deserialize<AiSettings>(json, JsonOptions.Settings);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.EncryptedServiceAccountJson);
    }

    [Fact]
    public void Json_round_trip_with_null_machine_model()
    {
        var original = new AiSettings { MachineModel = null };
        var json = JsonSerializer.Serialize(original, JsonOptions.Settings);
        var deserialized = JsonSerializer.Deserialize<AiSettings>(json, JsonOptions.Settings);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.MachineModel);
    }
}
