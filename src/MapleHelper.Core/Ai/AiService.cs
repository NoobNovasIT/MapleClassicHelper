using MapleHelper.Core.Settings;

namespace MapleHelper.Core.Ai;

public interface IAiService
{
    IAiProvider ActiveProvider { get; }
    Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken ct = default);
}

/// <summary>
/// Điểm vào duy nhất cho các feature (dịch, item, quest...). Tự lấy provider đang chọn,
/// key và model từ settings, feature không cần biết đang dùng AI nào.
/// </summary>
public sealed class AiService(
    IAiProviderRegistry registry,
    ISettingsService settings,
    ISecretStore secrets) : IAiService
{
    public IAiProvider ActiveProvider => registry.Get(settings.Current.Ai.ActiveProvider);

    public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken ct = default)
    {
        var provider = ActiveProvider;

        if (request.ImagePng is not null && !provider.Capabilities.HasFlag(AiCapabilities.Vision))
        {
            throw new AiException(AiErrorKind.NotSupported,
                $"{provider.DisplayName} không đọc được hình ảnh. Hãy chọn Gemini, OpenAI hoặc Claude trong Cài đặt.");
        }

        var apiKey = secrets.Get(SecretKeys.ApiKey(provider.Kind));
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiException(AiErrorKind.MissingApiKey,
                $"Chưa nhập API key cho {provider.DisplayName}. Vào Cài đặt để nhập key.");
        }

        var model = settings.Current.Ai.GetModel(provider.Kind);
        if (string.IsNullOrWhiteSpace(model))
        {
            model = provider.DefaultModel;
        }

        return provider.GenerateAsync(new AiConnection(apiKey, model), request, ct);
    }
}
