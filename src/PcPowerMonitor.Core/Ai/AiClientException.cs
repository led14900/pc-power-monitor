namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Thrown by <see cref="IAiClient"/> implementations (currently just
/// <see cref="VertexAiClient"/>) when the underlying API returns a non-success HTTP
/// status. Message is Vietnamese and user-facing (shown directly in the UI); the raw
/// status/body are kept on the exception for logging/debugging only.
/// </summary>
public class AiClientException : Exception
{
    /// <summary>HTTP status code returned by the AI API.</summary>
    public int StatusCode { get; }

    /// <summary>Raw (untruncated) response body, for diagnostics/logging only — never shown as-is in the UI.</summary>
    public string ResponseBody { get; }

    public AiClientException(int statusCode, string body)
        : base(FormatMessage(statusCode, body))
    {
        StatusCode = statusCode;
        ResponseBody = body;
    }

    private static string FormatMessage(int code, string body) => code switch
    {
        401 => "Thông tin xác thực Vertex AI không hợp lệ. Kiểm tra file service account JSON và Project ID.",
        403 => "Không có quyền truy cập Vertex AI. Kiểm tra quyền IAM của service account.",
        429 => "Đã vượt quá giới hạn gọi API Vertex AI. Vui lòng thử lại sau vài phút.",
        _ => $"Lỗi API Vertex AI ({code}): {Truncate(body, 200)}",
    };

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
}
