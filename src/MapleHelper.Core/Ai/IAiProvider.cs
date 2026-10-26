namespace MapleHelper.Core.Ai;

public interface IAiProvider
{
    AiProviderKind Kind { get; }
    string DisplayName { get; }
    AiCapabilities Capabilities { get; }
    string DefaultModel { get; }
    IReadOnlyList<string> SuggestedModels { get; }

    /// <summary>Trang để người dùng tạo API key.</summary>
    string ApiKeyUrl { get; }

    /// <summary>Lấy danh sách model. Dùng luôn để kiểm tra key mà không tốn token.</summary>
    Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default);

    Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default);

    /// <summary>
    /// Chọn model nên dùng trong danh sách thật của nhà cung cấp, dùng khi model đang cấu hình đã bị gỡ (404).
    /// </summary>
    string? PickPreferredModel(IReadOnlyList<string> available);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<IAiProvider> All { get; }
    IAiProvider Get(AiProviderKind kind);
}
