using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>Anthropic Claude Messages API (/v1/messages): hỗ trợ ảnh và server tool web_search.</summary>
public sealed class ClaudeProvider(HttpClient http) : AiProviderBase(http)
{
    private const string BaseUrl = "https://api.anthropic.com/v1";
    private const string ApiVersion = "2023-06-01";
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    /// <summary>web_search tạm dừng lượt (pause_turn) khi tra lâu; gửi tiếp tối đa số lần này.</summary>
    private const int MaxContinuations = 3;

    public override AiProviderKind Kind => AiProviderKind.Claude;
    public override string DisplayName => "Anthropic Claude";
    public override AiCapabilities Capabilities => AiCapabilities.Vision | AiCapabilities.WebSearch;
    public override string DefaultModel => "claude-opus-5-5";
    public override IReadOnlyList<string> SuggestedModels => ["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5"];
    public override string ApiKeyUrl => "https://console.anthropic.com/settings/keys";

    public override async Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default)
    {
        var json = await SendAsync(() => CreateRequest(HttpMethod.Get, "/models?limit=1000", apiKey, model: null), ct);
        return json["data"]?.AsArray().Select(m => m!["id"]!.GetValue<string>()).ToList() ?? [];
    }

    public override async Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default)
    {
        var body = BuildGenerateBody(connection.Model, request);
        var messages = body["messages"]!.AsArray();
        var collected = new ClaudeResult();

        for (var turn = 0; ; turn++)
        {
            var json = await SendAsync(() =>
            {
                var message = CreateRequest(HttpMethod.Post, "/messages", connection.ApiKey, connection.Model);
                message.Content = JsonContent(body);
                return message;
            }, ct);

            var stopReason = AppendResponse(json, collected);
            if (stopReason != "pause_turn" || turn >= MaxContinuations)
            {
                return collected.ToResponse();
            }

            // pause_turn: gửi lại nguyên nội dung assistant để Claude làm tiếp phần web search
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = json["content"]!.DeepClone() });
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey, string? model)
    {
        var message = new HttpRequestMessage(method, BaseUrl + path);
        message.Headers.Add("x-api-key", apiKey);
        message.Headers.Add("anthropic-version", ApiVersion);
        if (model is not null && SupportsServerFallback(model))
        {
            message.Headers.Add("anthropic-beta", FallbackBeta);
        }
        return message;
    }

    internal static bool SupportsServerFallback(string model) =>
        model is "claude-opus-5-5" or "claude-opus-5" or "claude-sonnet-5-5" or "claude-fable-5-1";

    /// <summary>Thế hệ 5.x nhận output_config.effort; Haiku 4.5 trở về trước sẽ báo lỗi nếu gửi.</summary>
    internal static bool SupportsEffort(string model) =>
        model.StartsWith("claude-opus-5", StringComparison.Ordinal)
        || model.StartsWith("claude-sonnet-5", StringComparison.Ordinal)
        || model.StartsWith("claude-haiku-5", StringComparison.Ordinal)
        || model.StartsWith("claude-fable-5", StringComparison.Ordinal);

    /// <summary>web_search bản 20260209 (lọc động) chỉ có trên Opus/Sonnet đời mới; Haiku dùng bản cơ bản.</summary>
    internal static string WebSearchToolType(string model) =>
        model.StartsWith("claude-haiku", StringComparison.Ordinal) ? "web_search_20250305" : "web_search_20260209";

    internal static JsonObject BuildGenerateBody(string model, AiRequest request)
    {
        var content = new JsonArray();
        if (request.ImagePng is not null)
        {
            content.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = "image/png",
                    ["data"] = Convert.ToBase64String(request.ImagePng),
                },
            });
        }
        content.Add(new JsonObject { ["type"] = "text", ["text"] = request.Prompt });

        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = request.MaxOutputTokens,
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = content } },
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system"] = request.SystemPrompt;
        }

        if (SupportsEffort(model))
        {
            // Overlay ưu tiên tốc độ: effort thấp cho dịch/tra cứu nhanh
            body["output_config"] = new JsonObject { ["effort"] = "low" };
        }

        if (SupportsServerFallback(model))
        {
            // Khi bị bộ lọc an toàn từ chối, server tự chuyển sang model dự phòng phù hợp
            body["fallbacks"] = "default";
        }

        if (request.UseWebSearch)
        {
            body["tools"] = new JsonArray
            {
                new JsonObject { ["type"] = WebSearchToolType(model), ["name"] = "web_search", ["max_uses"] = 5 },
            };
        }

        return body;
    }

    internal sealed class ClaudeResult
    {
        public List<string> Texts { get; } = [];
        public List<AiSource> Sources { get; } = [];

        public AiResponse ToResponse() =>
            new(string.Concat(Texts).Trim(), Sources.DistinctBy(s => s.Url).ToList());
    }

    /// <summary>Gom text và citation từ một response vào result. Trả về stop_reason.</summary>
    internal static string? AppendResponse(JsonNode json, ClaudeResult result)
    {
        var stopReason = json["stop_reason"]?.GetValue<string>();
        if (stopReason == "refusal")
        {
            var explanation = json["stop_details"]?["explanation"]?.GetValue<string>();
            throw new AiException(AiErrorKind.Refused, "Claude từ chối xử lý nội dung này." +
                (string.IsNullOrWhiteSpace(explanation) ? "" : " " + explanation));
        }

        foreach (var block in json["content"]?.AsArray() ?? [])
        {
            if (block?["type"]?.GetValue<string>() != "text")
            {
                continue; // thinking, server_tool_use, web_search_tool_result
            }

            result.Texts.Add(block["text"]?.GetValue<string>() ?? "");
            foreach (var citation in block["citations"]?.AsArray() ?? [])
            {
                var url = citation?["url"]?.GetValue<string>();
                if (url is not null)
                {
                    result.Sources.Add(new AiSource(citation!["title"]?.GetValue<string>() ?? "", url));
                }
            }
        }

        return stopReason;
    }
}
