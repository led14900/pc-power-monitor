using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// <see cref="IAiClient"/> implementation for Vertex AI, authenticated via a Google Cloud
/// service-account JSON key instead of a plain API key. Exchanges a JWT assertion (built
/// by <see cref="VertexServiceAccountCredential"/>) for a short-lived OAuth2 bearer token
/// and caches it until shortly before it expires. No Google.Apis/Google.Cloud NuGet
/// dependency — built entirely on .NET 8 BCL (<c>System.Security.Cryptography.RSA</c> +
/// <see cref="HttpClient"/>).
/// </summary>
public sealed class VertexAiClient : IAiClient
{
    // Static + shared: HttpClient is designed to be reused, not created per-request.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string TokenUrl = "https://oauth2.googleapis.com/token";

    // Refresh this many seconds before actual expiry, to avoid racing a request against
    // a token that expires mid-flight.
    private const int RefreshSkewSeconds = 300;

    private readonly string _projectId;
    private readonly string _region;
    private readonly string _modelId;
    private readonly VertexServiceAccountCredential _credential;

    private string? _cachedToken;
    private DateTime _tokenExpiryUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    /// <param name="serviceAccountJson">Full, decrypted contents of a Google Cloud service-account JSON key file.</param>
    /// <param name="projectId">Google Cloud project id owning the Vertex AI endpoint.</param>
    /// <param name="region">Vertex AI region (e.g. "us-central1").</param>
    /// <param name="modelId">Model id to call (e.g. "gemini-3.5-flash-lite").</param>
    /// <exception cref="ArgumentException">Service account JSON is malformed or missing required fields.</exception>
    public VertexAiClient(string serviceAccountJson, string projectId, string region, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAccountJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _projectId = projectId;
        _region = region;
        _modelId = modelId;
        _credential = VertexServiceAccountCredential.Parse(serviceAccountJson);
    }

    /// <inheritdoc cref="IAiClient.GenerateAsync"/>
    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));

        var token = await GetAccessTokenAsync(ct).ConfigureAwait(false);
        // The "global" location is special-cased by Google: unlike every other region
        // ("us-central1", "europe-west1", ...) it has NO region-prefixed host — the
        // hostname stays plain "aiplatform.googleapis.com". Prefixing it anyway (i.e.
        // "global-aiplatform.googleapis.com") resolves to nothing real and comes back
        // as a generic Google Frontend 404 HTML page instead of a JSON API error.
        var host = string.Equals(_region, "global", StringComparison.OrdinalIgnoreCase)
            ? "aiplatform.googleapis.com"
            : $"{_region}-aiplatform.googleapis.com";
        var url = $"https://{host}/v1/projects/{_projectId}/locations/{_region}" +
                  $"/publishers/google/models/{_modelId}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new
        {
            // Vertex requires an explicit "role" per content entry ("user"/"model") —
            // omitting it fails validation with a 400 INVALID_ARGUMENT ("Please use a
            // valid role: user, model."), unlike AI Studio which defaulted it for
            // single-turn requests.
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            // Vertex uses camelCase tool names, unlike AI Studio's snake_case google_search.
            tools = new[] { new { googleSearch = new { } } },
        };
        request.Content = JsonContent.Create(body);

        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new AiClientException((int)response.StatusCode, error);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        return AiResponseEnvelope.ExtractText(json);
    }

    /// <summary>Thread-safe, lazy-refresh token cache: SemaphoreSlim + double-check so concurrent callers share one token fetch.</summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiryUtc.AddSeconds(-RefreshSkewSeconds))
            return _cachedToken;

        await _tokenLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiryUtc.AddSeconds(-RefreshSkewSeconds))
                return _cachedToken;

            var (token, expiresIn) = await ExchangeJwtForTokenAsync(ct).ConfigureAwait(false);
            _cachedToken = token;
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>POSTs the signed JWT assertion to Google's OAuth2 token endpoint per the JWT Bearer flow.</summary>
    private async Task<(string Token, int ExpiresIn)> ExchangeJwtForTokenAsync(CancellationToken ct)
    {
        var assertion = _credential.BuildJwtAssertion();
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
        };

        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new AiClientException((int)response.StatusCode, error);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var token = json.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
        if (string.IsNullOrEmpty(token))
            throw new AiClientException(500, "Không nhận được access token từ Google OAuth2.");

        var expiresIn = json.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;
        return (token, expiresIn);
    }

    public void Dispose()
    {
        _credential.Dispose();
        _tokenLock.Dispose();
    }
}
