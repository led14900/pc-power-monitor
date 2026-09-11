using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Parses a Google Cloud service-account JSON key file and signs the self-issued JWT
/// assertion used by the <see href="https://developers.google.com/identity/protocols/oauth2/service-account#jwt-auth">
/// JWT Bearer flow</see>. Split out of <see cref="VertexAiClient"/> to keep that class
/// focused on the HTTP/token-cache concerns; this class owns credential parsing + RSA
/// signing only.
/// </summary>
internal sealed class VertexServiceAccountCredential : IDisposable
{
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string TokenScope = "https://www.googleapis.com/auth/cloud-platform";

    private readonly RSA _rsa;

    public string ServiceAccountEmail { get; }

    private VertexServiceAccountCredential(string serviceAccountEmail, RSA rsa)
    {
        ServiceAccountEmail = serviceAccountEmail;
        _rsa = rsa;
    }

    /// <param name="serviceAccountJson">Full, decrypted contents of a Google Cloud service-account JSON key file.</param>
    /// <exception cref="ArgumentException">JSON is malformed or missing client_email/private_key.</exception>
    public static VertexServiceAccountCredential Parse(string serviceAccountJson)
    {
        ServiceAccountKey key;
        try
        {
            key = JsonSerializer.Deserialize<ServiceAccountKey>(serviceAccountJson)
                  ?? throw new ArgumentException("Service account JSON rỗng hoặc không hợp lệ.", nameof(serviceAccountJson));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Service account JSON không đúng định dạng.", nameof(serviceAccountJson), ex);
        }

        if (string.IsNullOrWhiteSpace(key.ClientEmail) || string.IsNullOrWhiteSpace(key.PrivateKey))
            throw new ArgumentException(
                "Service account JSON thiếu trường \"client_email\" hoặc \"private_key\".", nameof(serviceAccountJson));

        // System.Text.Json already unescapes \n into real newlines when it deserializes
        // the JSON string, so private_key here is a standard multi-line PEM block —
        // ImportFromPem works directly, no manual unescaping needed.
        var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKey);
        return new VertexServiceAccountCredential(key.ClientEmail, rsa);
    }

    /// <summary>Builds and RS256-signs a self-issued JWT assertion per Google's service-account JWT auth spec.</summary>
    public string BuildJwtAssertion()
    {
        var now = DateTimeOffset.UtcNow;
        var header = new { alg = "RS256", typ = "JWT" };
        var payload = new
        {
            iss = ServiceAccountEmail,
            scope = TokenScope,
            aud = TokenUrl,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(1).ToUnixTimeSeconds(),
        };

        var unsigned = $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header))}." +
                       $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload))}";

        var signature = _rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _rsa.Dispose();

    /// <summary>Minimal subset of a Google Cloud service-account JSON key file that this client actually needs.</summary>
    private sealed class ServiceAccountKey
    {
        [JsonPropertyName("client_email")]
        public string? ClientEmail { get; set; }

        [JsonPropertyName("private_key")]
        public string? PrivateKey { get; set; }
    }
}
