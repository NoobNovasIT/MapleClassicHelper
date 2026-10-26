using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapleHelper.Core.Features.Translation;

namespace MapleHelper.Infrastructure.Translation;

/// <summary>
/// Google Dịch qua endpoint công khai translate.googleapis.com (client=gtx), không cần API key.
/// Không có cam kết chính thức: nếu bị chặn do gọi quá nhiều, chuyển sang Cloud Translation API.
/// </summary>
public sealed class GoogleTranslateClient(HttpClient http) : IMachineTranslator
{
    private const string Endpoint = "https://translate.googleapis.com/translate_a/single";

    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default)
    {
        // POST để câu dài không vượt giới hạn độ dài URL
        var url = $"{Endpoint}?client=gtx&dt=t&sl={Uri.EscapeDataString(from)}&tl={Uri.EscapeDataString(to)}";
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", text)]);

        string body;
        try
        {
            using var response = await http.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new MachineTranslationException(
                    $"Google Dịch báo lỗi ({(int)response.StatusCode}). Có thể đã gọi quá nhiều, thử lại sau ít phút.");
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

        return ParseResponse(body);
    }

    /// <summary>
    /// Kết quả dạng [[["câu dịch","câu gốc",...],["câu dịch 2",...]], null, "vi", ...].
    /// Mỗi câu một phần tử, nối lại theo thứ tự.
    /// </summary>
    internal static string ParseResponse(string body)
    {
        try
        {
            if (JsonNode.Parse(body) is not JsonArray { Count: > 0 } root || root[0] is not JsonArray segments)
            {
                throw new MachineTranslationException("Google Dịch trả về dữ liệu không đọc được.");
            }
            var sb = new StringBuilder();
            foreach (var segment in segments)
            {
                if (segment?[0] is JsonValue value && value.TryGetValue<string>(out var part))
                {
                    sb.Append(part);
                }
            }
            return sb.ToString().Trim();
        }
        catch (JsonException ex)
        {
            throw new MachineTranslationException("Google Dịch trả về dữ liệu không đọc được.", ex);
        }
    }
}
