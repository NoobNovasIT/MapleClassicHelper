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
    private readonly SemaphoreSlim _modelRecovery = new(1, 1);

    public IAiProvider ActiveProvider => registry.Get(settings.Current.Ai.ActiveProvider);

    public async Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken ct = default)
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

        var model = CurrentModel(provider);
        try
        {
            return await provider.GenerateAsync(new AiConnection(apiKey, model), request, ct);
        }
        catch (AiException ex) when (ex.Kind == AiErrorKind.ModelNotFound)
        {
            // Nhà cung cấp gỡ model cũ (vd. gemini-2.5-flash): tự chọn model thay thế rồi thử lại một lần
            var replacement = await RecoverModelAsync(provider, apiKey, model, ct);
            if (replacement is null)
            {
                throw;
            }
            return await provider.GenerateAsync(new AiConnection(apiKey, replacement), request, ct);
        }
    }

    private string CurrentModel(IAiProvider provider)
    {
        var model = settings.Current.Ai.GetModel(provider.Kind);
        return string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model;
    }

    private async Task<string?> RecoverModelAsync(IAiProvider provider, string apiKey, string failedModel, CancellationToken ct)
    {
        await _modelRecovery.WaitAsync(ct);
        try
        {
            // Request song song khác có thể đã đổi model trong lúc chờ
            var current = CurrentModel(provider);
            if (current != failedModel)
            {
                return current;
            }

            var available = await provider.ListModelsAsync(apiKey, ct);
            var replacement = provider.PickPreferredModel(available);
            if (replacement is null || replacement == failedModel)
            {
                return null;
            }
            settings.Current.Ai.Models[provider.Kind] = replacement;
            await settings.SaveAsync(ct);
            return replacement;
        }
        finally
        {
            _modelRecovery.Release();
        }
    }
}
