using System.Net;
using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>Google Gemini (generativelanguage REST v1beta). Grounding qua tool google_search.</summary>
public sealed class GeminiProvider(HttpClient http) : AiProviderBase(http)
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    public override AiProviderKind Kind => AiProviderKind.Gemini;
    public override string DisplayName => "Google Gemini";
    public override AiCapabilities Capabilities => AiCapabilities.Vision | AiCapabilities.WebSearch;
    public override string DefaultModel => "gemini-2.5-flash";
    public override IReadOnlyList<string> SuggestedModels => ["gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.5-pro"];
    public override string ApiKeyUrl => "https://aistudio.google.com/apikey";

    public override async Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var json = await SendAsync(() => CreateRequest(HttpMethod.Get, "/models?pageSize=1000", apiKey), ct);
        return ParseModelList(json);
    }

    public override async Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default)
    {
        var body = BuildGenerateBody(request);
        var json = await SendAsync(() =>
        {
            var message = CreateRequest(HttpMethod.Post, $"/models/{connection.Model}:generateContent", connection.ApiKey);
            message.Content = JsonContent(body);
            return message;
        }, ct);
        return ParseGenerateResponse(json);
    }

    protected override AiException MapError(HttpStatusCode status, string body)
    {
        // Gemini trả 400 (không phải 401) khi key sai
        if (status == HttpStatusCode.BadRequest && body.Contains("API_KEY_INVALID", StringComparison.Ordinal))
        {
            return new AiException(AiErrorKind.InvalidApiKey, $"API key {DisplayName} không hợp lệ.");
        }
        return base.MapError(status, body);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey)
    {
        var message = new HttpRequestMessage(method, BaseUrl + path);
        message.Headers.Add("x-goog-api-key", apiKey);
        return message;
    }

    internal static JsonObject BuildGenerateBody(AiRequest request)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = request.Prompt } };
        if (request.ImagePng is not null)
        {
            parts.Add(new JsonObject
            {
                ["inline_data"] = new JsonObject
                {
                    ["mime_type"] = "image/png",
                    ["data"] = Convert.ToBase64String(request.ImagePng),
                },
            });
        }

        var body = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = parts } },
            ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = request.MaxOutputTokens },
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } },
            };
        }

        if (request.UseWebSearch)
        {
            body["tools"] = new JsonArray { new JsonObject { ["google_search"] = new JsonObject() } };
        }

        return body;
    }

    internal static IReadOnlyList<string> ParseModelList(JsonNode json) =>
        json["models"]?.AsArray()
            .Where(m => m?["supportedGenerationMethods"]?.AsArray()
                .Any(x => x?.GetValue<string>() == "generateContent") == true)
            .Select(m => m!["name"]!.GetValue<string>().Replace("models/", "", StringComparison.Ordinal))
            .Order()
            .ToList()
        ?? [];

    internal static AiResponse ParseGenerateResponse(JsonNode json)
    {
        var blockReason = json["promptFeedback"]?["blockReason"]?.GetValue<string>();
        if (blockReason is not null)
        {
            throw new AiException(AiErrorKind.Refused, $"Gemini từ chối xử lý nội dung ({blockReason}).");
        }

        var candidate = json["candidates"]?.AsArray().FirstOrDefault()
            ?? throw new AiException(AiErrorKind.Unknown, "Gemini không trả về kết quả.");

        var finishReason = candidate["finishReason"]?.GetValue<string>();
        if (finishReason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "RECITATION")
        {
            throw new AiException(AiErrorKind.Refused, $"Gemini từ chối xử lý nội dung ({finishReason}).");
        }

        // Bỏ qua phần "thought" của model có thinking, chỉ lấy câu trả lời
        var text = string.Concat(
            candidate["content"]?["parts"]?.AsArray()
                .Where(p => p?["thought"]?.GetValue<bool>() != true)
                .Select(p => p?["text"]?.GetValue<string>() ?? "")
            ?? []);

        var sources = candidate["groundingMetadata"]?["groundingChunks"]?.AsArray()
            .Select(c => c?["web"])
            .Where(w => w?["uri"] is not null)
            .Select(w => new AiSource(w!["title"]?.GetValue<string>() ?? "", w["uri"]!.GetValue<string>()))
            .DistinctBy(s => s.Url)
            .ToList()
            ?? [];

        return new AiResponse(text.Trim(), sources);
    }
}
