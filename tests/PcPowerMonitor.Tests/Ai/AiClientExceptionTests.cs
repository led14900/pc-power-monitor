using PcPowerMonitor.Core.Ai;

namespace PcPowerMonitor.Tests.Ai;

public sealed class AiClientExceptionTests
{
    [Fact]
    public void StatusCode_401_returns_vietnamese_key_error()
    {
        var ex = new AiClientException(401, "Unauthorized");

        Assert.Contains("Thông tin xác thực Vertex AI không hợp lệ", ex.Message);
    }

    [Fact]
    public void StatusCode_403_returns_vietnamese_permission_error()
    {
        var ex = new AiClientException(403, "Forbidden");

        Assert.Contains("quyền truy cập", ex.Message);
    }

    [Fact]
    public void StatusCode_429_returns_vietnamese_rate_limit()
    {
        var ex = new AiClientException(429, "Rate limited");

        Assert.Contains("giới hạn", ex.Message);
    }

    [Fact]
    public void Other_status_includes_code_and_truncated_body()
    {
        var ex = new AiClientException(500, new string('x', 300));

        Assert.Contains("500", ex.Message);
        Assert.True(ex.Message.Length < 250, $"Expected truncated message, got length {ex.Message.Length}");
    }

    [Fact]
    public void Other_status_body_shorter_than_limit_is_not_truncated()
    {
        var ex = new AiClientException(503, "Service unavailable");

        Assert.Contains("Service unavailable", ex.Message);
        Assert.DoesNotContain("...", ex.Message);
    }

    [Fact]
    public void StatusCode_and_ResponseBody_are_exposed_untruncated()
    {
        var body = new string('y', 300);
        var ex = new AiClientException(500, body);

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal(body, ex.ResponseBody);
    }

    [Fact]
    public void Empty_body_for_other_status_does_not_throw()
    {
        var ex = new AiClientException(400, string.Empty);

        Assert.Contains("400", ex.Message);
    }
}
