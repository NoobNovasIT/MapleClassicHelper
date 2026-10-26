using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>
/// Phần dùng chung của các REST client: gửi JSON, retry lỗi tạm thời (429/5xx), chuẩn hoá lỗi thành <see cref="AiException"/>.
/// </summary>
public abstract class AiProviderBase(HttpClient http) : IAiProvider
{
    private const int MaxRetries = 2;

    public abstract AiProviderKind Kind { get; }
    public abstract string DisplayName { get; }
    public abstract AiCapabilities Capabilities { get; }
    public abstract string DefaultModel { get; }
    public abstract IReadOnlyList<string> SuggestedModels { get; }
    public abstract string ApiKeyUrl { get; }

    public abstract Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default);
    public abstract Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default);

    protected static HttpContent JsonContent(JsonNode body) =>
        new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

    /// <summary>createRequest được gọi lại mỗi lần retry vì HttpRequestMessage không gửi lại được.</summary>
    protected async Task<JsonNode> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = createRequest();
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxRetries)
                {
                    await Task.Delay(BackoffDelay(attempt), ct);
                    continue;
                }
                throw new AiException(AiErrorKind.Network, $"Không kết nối được tới {DisplayName}. Kiểm tra mạng.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new AiException(AiErrorKind.Network, $"{DisplayName} phản hồi quá lâu (timeout).", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return JsonNode.Parse(body) ?? throw new AiException(AiErrorKind.Unknown, $"{DisplayName} trả về dữ liệu rỗng.");
                }

                if (IsTransient(response.StatusCode) && attempt < MaxRetries)
                {
                    await Task.Delay(response.Headers.RetryAfter?.Delta ?? BackoffDelay(attempt), ct);
                    continue;
                }

                throw MapError(response.StatusCode, body);
            }
        }
    }

    protected virtual AiException MapError(HttpStatusCode status, string body)
    {
        var detail = ExtractErrorMessage(body);
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AiException(AiErrorKind.InvalidApiKey, $"API key {DisplayName} không hợp lệ hoặc không có quyền."),
            HttpStatusCode.NotFound =>
                new AiException(AiErrorKind.ModelNotFound, $"Không tìm thấy model trên {DisplayName}. Kiểm tra lại tên model trong Cài đặt."),
            HttpStatusCode.TooManyRequests =>
                new AiException(AiErrorKind.QuotaExceeded, $"Đã vượt giới hạn request hoặc quota của {DisplayName}. Thử lại sau ít phút."),
            HttpStatusCode.PaymentRequired =>
                new AiException(AiErrorKind.QuotaExceeded, $"Tài khoản {DisplayName} đã hết số dư."),
            _ => new AiException(AiErrorKind.Unknown, $"{DisplayName} báo lỗi ({(int)status}): {detail}"),
        };
    }

    /// <summary>Cả 4 provider đều trả lỗi dạng {"error": {"message": "..."}}.</summary>
    internal static string ExtractErrorMessage(string body)
    {
        try
        {
            var message = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch (Exception)
        {
            // Body không phải JSON (thường là trang lỗi HTML từ proxy)
        }
        return body.Length > 300 ? body[..300] : body;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or (HttpStatusCode)529; // Anthropic: overloaded

    private static TimeSpan BackoffDelay(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt));
}
