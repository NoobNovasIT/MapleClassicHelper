using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>OpenAI Responses API (/v1/responses): hỗ trợ ảnh và tool web_search.</summary>
public sealed class OpenAiProvider(HttpClient http) : AiProviderBase(http)
{
    private const string BaseUrl = "https://api.openai.com/v1";

    private static readonly string[] NonChatMarkers = ["audio", "realtime", "tts", "transcribe", "image", "embedding", "moderation"];

    public override AiProviderKind Kind => AiProviderKind.OpenAi;
    public override string DisplayName => "OpenAI";
    public override AiCapabilities Capabilities => AiCapabilities.Vision | AiCapabilities.WebSearch;
    public override string DefaultModel => "gpt-5-mini";
    public override IReadOnlyList<string> SuggestedModels => ["gpt-5-mini", "gpt-5-nano", "gpt-5"];
    public override string ApiKeyUrl => "https://platform.openai.com/api-keys";

    public override async Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var json = await SendAsync(() => CreateRequest(HttpMethod.Get, "/models", apiKey), ct);
        return ParseModelList(json);
    }

    public override async Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default)
    {
        var body = BuildGenerateBody(connection.Model, request);
        var json = await SendAsync(() =>
        {
            var message = CreateRequest(HttpMethod.Post, "/responses", connection.ApiKey);
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

    /// <summary>Họ model có reasoning (gpt-5*, o*) nhận tham số reasoning; model khác sẽ báo lỗi 400 nếu gửi.</summary>
    internal static bool IsReasoningModel(string model) =>
        model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
        || (model.Length > 1 && model[0] == 'o' && char.IsDigit(model[1]));

    internal static JsonObject BuildGenerateBody(string model, AiRequest request)
    {
        var content = new JsonArray { new JsonObject { ["type"] = "input_text", ["text"] = request.Prompt } };
        if (request.ImagePng is not null)
        {
            content.Add(new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = "data:image/png;base64," + Convert.ToBase64String(request.ImagePng),
            });
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["input"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = content } },
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["store"] = false,
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["instructions"] = request.SystemPrompt;
        }

        if (IsReasoningModel(model))
        {
            // Overlay cần phản hồi nhanh, reasoning thấp là đủ cho dịch/tra cứu
            body["reasoning"] = new JsonObject { ["effort"] = "low" };
        }

        if (request.UseWebSearch)
        {
            body["tools"] = new JsonArray { new JsonObject { ["type"] = "web_search" } };
        }

        return body;
    }

    internal static IReadOnlyList<string> ParseModelList(JsonNode json) =>
        json["data"]?.AsArray()
            .Select(m => m!["id"]!.GetValue<string>())
            .Where(id => (id.StartsWith("gpt-", StringComparison.Ordinal) || IsReasoningModel(id))
                         && !NonChatMarkers.Any(marker => id.Contains(marker, StringComparison.Ordinal)))
            .Order()
            .ToList()
        ?? [];

    internal static AiResponse ParseGenerateResponse(JsonNode json)
    {
        var texts = new List<string>();
        var sources = new List<AiSource>();

        foreach (var item in json["output"]?.AsArray() ?? [])
        {
            if (item?["type"]?.GetValue<string>() != "message")
            {
                continue; // reasoning, web_search_call...
            }

            foreach (var part in item["content"]?.AsArray() ?? [])
            {
                switch (part?["type"]?.GetValue<string>())
                {
                    case "output_text":
                        texts.Add(part["text"]?.GetValue<string>() ?? "");
                        foreach (var annotation in part["annotations"]?.AsArray() ?? [])
                        {
                            if (annotation?["type"]?.GetValue<string>() == "url_citation")
                            {
                                sources.Add(new AiSource(
                                    annotation["title"]?.GetValue<string>() ?? "",
                                    annotation["url"]!.GetValue<string>()));
                            }
                        }
                        break;
                    case "refusal":
                        throw new AiException(AiErrorKind.Refused,
                            "OpenAI từ chối xử lý: " + (part["refusal"]?.GetValue<string>() ?? ""));
                }
            }
        }

        var text = string.Concat(texts).Trim();
        if (text.Length == 0 && json["status"]?.GetValue<string>() == "incomplete")
        {
            var reason = json["incomplete_details"]?["reason"]?.GetValue<string>();
            throw new AiException(AiErrorKind.Unknown, $"OpenAI dừng giữa chừng ({reason}). Thử tăng giới hạn token hoặc đổi model.");
        }

        return new AiResponse(text, sources.DistinctBy(s => s.Url).ToList());
    }
}
