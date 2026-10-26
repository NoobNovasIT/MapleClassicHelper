namespace MapleHelper.Core.Ai;

public enum AiProviderKind
{
    Gemini,
    OpenAi,
    Claude,
    DeepSeek,
}

[Flags]
public enum AiCapabilities
{
    None = 0,
    Vision = 1,
    WebSearch = 2,
}

/// <summary>Thông tin kết nối truyền vào provider mỗi lần gọi. Provider không tự giữ key.</summary>
public sealed record AiConnection(string ApiKey, string Model);

public sealed record AiRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }

    /// <summary>Ảnh PNG (vùng chụp màn hình). Null nếu chỉ gửi text.</summary>
    public byte[]? ImagePng { get; init; }

    /// <summary>Cho phép model tự tra Google/web (Gemini grounding, OpenAI/Claude web search).</summary>
    public bool UseWebSearch { get; init; }

    public int MaxOutputTokens { get; init; } = 4096;
}

public sealed record AiSource(string Title, string Url);

public sealed record AiResponse(string Text, IReadOnlyList<AiSource> Sources)
{
    public static AiResponse FromText(string text) => new(text, []);
}
