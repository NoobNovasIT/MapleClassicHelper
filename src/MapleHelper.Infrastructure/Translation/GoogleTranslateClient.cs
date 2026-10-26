using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapleHelper.Core.Features.Translation;
using Microsoft.Extensions.Logging;

namespace MapleHelper.Infrastructure.Translation;

/// <summary>
/// Google Dịch qua các endpoint công khai không cần API key. Mỗi endpoint có giới hạn tần suất riêng
/// (không công bố): endpoint nào trả 429 thì tạm nghỉ vài phút và chuyển sang endpoint còn lại.
/// </summary>
public sealed class GoogleTranslateClient(HttpClient http, ILogger<GoogleTranslateClient>? logger = null) : IMachineTranslator
{
    internal enum ResponseFormat
    {
        /// <summary>[[["câu dịch","câu gốc",...],...], null, "vi", ...]</summary>
        Gtx,

        /// <summary>["câu dịch"] hoặc [["câu dịch","en"]] khi sl=auto</summary>
        DictChrome,
    }

    internal sealed record Endpoint(string Name, string BaseUrl, ResponseFormat Format)
    {
        public DateTimeOffset BlockedUntil { get; set; }
    }

    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    // Static: mọi instance (typed HttpClient tạo mới theo scope) dùng chung trạng thái bị chặn
    private static readonly Endpoint[] Endpoints =
    [
        new("translate.googleapis.com", "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t", ResponseFormat.Gtx),
        new("clients5.google.com", "https://clients5.google.com/translate_a/t?client=dict-chrome-ex", ResponseFormat.DictChrome),
    ];

    // Một request mỗi lúc: tránh dịch tin chạy nền gọi dồn cùng lúc với chat
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false)
    {
        await Gate.WaitAsync(ct);
        try
        {
            MachineTranslationException? lastError = null;
            foreach (var endpoint in background ? Endpoints[..1] : Endpoints)
            {
                if (endpoint.BlockedUntil > DateTimeOffset.UtcNow)
                {
                    continue;
                }
                try
                {
                    return await TranslateWithAsync(endpoint, text, from, to, ct);
                }
                catch (MachineTranslationRateLimitedException ex)
                {
                    endpoint.BlockedUntil = DateTimeOffset.UtcNow + Cooldown;
                    logger?.LogWarning("Google Dịch {Endpoint} bị giới hạn tần suất, tạm nghỉ {Minutes} phút", endpoint.Name, Cooldown.TotalMinutes);
                    lastError = ex;
                }
                catch (MachineTranslationException ex) when (ex.InnerException is TaskCanceledException)
                {
                    // Bị chặn kiểu "treo" thay vì trả 429: nghỉ ngắn rồi thử endpoint khác
                    endpoint.BlockedUntil = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
                    logger?.LogWarning("Google Dịch {Endpoint} không phản hồi, chuyển endpoint khác", endpoint.Name);
                    lastError = ex;
                }
            }

            throw lastError ?? new MachineTranslationRateLimitedException(
                "Google Dịch đang tạm giới hạn vì có quá nhiều yêu cầu. Thử lại sau khoảng 5 phút.");
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<string> TranslateWithAsync(Endpoint endpoint, string text, string from, string to, CancellationToken ct)
    {
        // POST để câu dài không vượt giới hạn độ dài URL
        var url = $"{endpoint.BaseUrl}&sl={Uri.EscapeDataString(from)}&tl={Uri.EscapeDataString(to)}";
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", text)]);

        string body;
        try
        {
            using var response = await http.PostAsync(url, content, ct);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                throw new MachineTranslationRateLimitedException(
                    "Google Dịch đang tạm giới hạn vì có quá nhiều yêu cầu. Thử lại sau khoảng 5 phút.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new MachineTranslationException($"Google Dịch báo lỗi ({(int)response.StatusCode}).");
            }
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new MachineTranslationException("Không kết nối được Google Dịch. Kiểm tra mạng.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new MachineTranslationException("Google Dịch phản hồi quá lâu.", ex);
        }

        return ParseResponse(body, endpoint.Format);
    }

    internal static string ParseResponse(string body) => ParseResponse(body, ResponseFormat.Gtx);

    internal static string ParseResponse(string body, ResponseFormat format)
    {
        try
        {
            if (JsonNode.Parse(body) is not JsonArray { Count: > 0 } root)
            {
                throw Unreadable();
            }

            var sb = new StringBuilder();
            if (format == ResponseFormat.Gtx)
            {
                // Mỗi câu một phần tử, nối lại theo thứ tự
                if (root[0] is not JsonArray segments)
                {
                    throw Unreadable();
                }
                foreach (var segment in segments)
                {
                    if (segment?[0] is JsonValue value && value.TryGetValue<string>(out var part))
                    {
                        sb.Append(part);
                    }
                }
            }
            else
            {
                foreach (var item in root)
                {
                    var node = item is JsonArray pair ? pair[0] : item;
                    if (node is JsonValue value && value.TryGetValue<string>(out var part))
                    {
                        sb.Append(part);
                    }
                }
            }
            return sb.ToString().Trim();
        }
        catch (JsonException ex)
        {
            throw new MachineTranslationException("Google Dịch trả về dữ liệu không đọc được.", ex);
        }
    }

    private static MachineTranslationException Unreadable() => new("Google Dịch trả về dữ liệu không đọc được.");

    /// <summary>Chỉ dùng cho test: xoá trạng thái tạm chặn.</summary>
    internal static void ResetCooldowns()
    {
        foreach (var endpoint in Endpoints)
        {
            endpoint.BlockedUntil = default;
        }
    }
}
