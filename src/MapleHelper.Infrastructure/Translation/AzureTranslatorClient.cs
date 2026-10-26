using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.Settings;

namespace MapleHelper.Infrastructure.Translation;

/// <summary>
/// Microsoft Azure AI Translator (REST v3). Gói F0 miễn phí 2 triệu ký tự/tháng, hết quota thì báo lỗi chứ không tính tiền.
/// Key và region người dùng tự tạo trên Azure portal.
/// </summary>
public sealed class AzureTranslatorClient(HttpClient http, ISecretStore secrets, ISettingsService settings) : IMachineTranslator
{
    private const string Endpoint = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(secrets.Get(SecretKeys.AzureTranslatorKey));

    public Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false) =>
        TranslateWithKeyAsync(secrets.Get(SecretKeys.AzureTranslatorKey) ?? "", settings.Current.Translator.AzureRegion, text, from, to, ct);

    /// <summary>Dùng trực tiếp key/region đang nhập trong Cài đặt (nút Kiểm tra), chưa cần lưu.</summary>
    public async Task<string> TranslateWithKeyAsync(string key, string region, string text, string from, string to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new MachineTranslationException("Chưa nhập key Azure Translator trong Cài đặt.");
        }

        // Bỏ "from" để Azure tự nhận ngôn ngữ
        var url = Endpoint + (from is "" or "auto" ? "" : "&from=" + Uri.EscapeDataString(from)) + "&to=" + Uri.EscapeDataString(to);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(new JsonArray(new JsonObject { ["Text"] = text }).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", key.Trim());
        if (!string.IsNullOrWhiteSpace(region))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Region", region.Trim());
        }

        string body;
        try
        {
            using var response = await http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw MapError(response.StatusCode, body);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new MachineTranslationException("Không kết nối được Azure Translator. Kiểm tra mạng.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new MachineTranslationException("Azure Translator phản hồi quá lâu.", ex);
        }

        return ParseResponse(body);
    }

    internal static string ParseResponse(string body)
    {
        try
        {
            var text = JsonNode.Parse(body)?[0]?["translations"]?[0]?["text"]?.GetValue<string>();
            return text?.Trim() ?? throw new MachineTranslationException("Azure Translator trả về dữ liệu không đọc được.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new MachineTranslationException("Azure Translator trả về dữ liệu không đọc được.", ex);
        }
    }

    /// <summary>Lỗi Azure: 401 key hoặc region sai, 403 mã 403001 hết quota tháng, 429 gọi quá nhanh.</summary>
    internal static MachineTranslationException MapError(HttpStatusCode status, string body)
    {
        var code = 0;
        try
        {
            code = JsonNode.Parse(body)?["error"]?["code"]?.GetValue<int>() ?? 0;
        }
        catch (Exception)
        {
            // Body không phải JSON
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => new MachineTranslationException(
                "Key Azure Translator không hợp lệ hoặc sai region. Kiểm tra lại trong Cài đặt."),
            HttpStatusCode.Forbidden when code is 403001 => new MachineTranslationRateLimitedException(
                "Azure Translator đã dùng hết 2 triệu ký tự miễn phí của tháng này."),
            HttpStatusCode.TooManyRequests => new MachineTranslationRateLimitedException(
                "Azure Translator đang giới hạn vì gọi quá nhanh, thử lại sau ít giây."),
            _ => new MachineTranslationException($"Azure Translator báo lỗi ({(int)status}{(code > 0 ? $", mã {code}" : "")})."),
        };
    }
}

/// <summary>
/// Thứ tự: Google (2 endpoint miễn phí) → Azure Translator nếu đã nhập key.
/// Việc chạy nền (dịch tin) không dùng Azure để quota dành cho chat và dịch vùng chọn.
/// </summary>
public sealed class FallbackMachineTranslator(GoogleTranslateClient google, AzureTranslatorClient azure) : IMachineTranslator
{
    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false)
    {
        try
        {
            return await google.TranslateAsync(text, from, to, ct, background);
        }
        catch (MachineTranslationException googleError) when (!background && !ct.IsCancellationRequested)
        {
            if (!azure.IsConfigured)
            {
                throw new MachineTranslationException(googleError.Message +
                    " Nhập key Azure Translator (miễn phí) trong Cài đặt để app tự chuyển sang khi Google bị chặn.", googleError);
            }
            return await azure.TranslateAsync(text, from, to, ct);
        }
    }
}
