using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>DeepSeek (API tương thích OpenAI Chat Completions). Chỉ xử lý text: không đọc ảnh, không web search.</summary>
public sealed class DeepSeekProvider(HttpClient http) : AiProviderBase(http)
{
    private const string BaseUrl = "https://api.deepseek.com";

    public override AiProviderKind Kind => AiProviderKind.DeepSeek;
    public override string DisplayName => "DeepSeek";
    public override AiCapabilities Capabilities => AiCapabilities.None;
    public override string DefaultModel => "deepseek-chat";
    public override IReadOnlyList<string> SuggestedModels => ["deepseek-chat", "deepseek-reasoner"];
    public override string ApiKeyUrl => "https://platform.deepseek.com/api_keys";

    public override async Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var json = await SendAsync(() => CreateRequest(HttpMethod.Get, "/models", apiKey), ct);
        return json["data"]?.AsArray().Select(m => m!["id"]!.GetValue<string>()).Order().ToList() ?? [];
    }

    public override async Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default)
    {
        if (request.ImagePng is not null)
        {
            throw new AiException(AiErrorKind.NotSupported, "DeepSeek không đọc được hình ảnh.");
        }

        var body = BuildGenerateBody(connection.Model, request);
        var json = await SendAsync(() =>
        {
            var message = CreateRequest(HttpMethod.Post, "/chat/completions", connection.ApiKey);
            message.Content = JsonContent(body);
            return message;
        }, ct);
        return ParseGenerateResponse(json);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey)
    {
        var message = new HttpRequestMessage(method, BaseUrl + path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return message;
    }

    internal static JsonObject BuildGenerateBody(string model, AiRequest request)
    {
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });
        }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.Prompt });

        return new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["max_tokens"] = request.MaxOutputTokens,
            ["stream"] = false,
        };
    }

    internal static AiResponse ParseGenerateResponse(JsonNode json)
    {
        var choice = json["choices"]?.AsArray().FirstOrDefault()
            ?? throw new AiException(AiErrorKind.Unknown, "DeepSeek không trả về kết quả.");

        if (choice["finish_reason"]?.GetValue<string>() == "content_filter")
        {
            throw new AiException(AiErrorKind.Refused, "DeepSeek từ chối xử lý nội dung này.");
        }

        var text = choice["message"]?["content"]?.GetValue<string>() ?? "";
        return AiResponse.FromText(text.Trim());
    }
}
