using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.Core.Ai;
using MapleHelper.Core.Settings;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App.Features.Settings;

public enum StatusKind
{
    None,
    Info,
    Success,
    Error,
}

/// <summary>Thông tin hiển thị của một provider. Không giữ instance provider (typed HttpClient).</summary>
public sealed record ProviderOption(
    AiProviderKind Kind,
    string DisplayName,
    string DefaultModel,
    IReadOnlyList<string> SuggestedModels,
    string ApiKeyUrl,
    AiCapabilities Capabilities)
{
    public string CapabilityText => Capabilities.HasFlag(AiCapabilities.Vision)
        ? "Đọc ảnh, tìm kiếm web: dùng được cả 4 nút overlay"
        : "Chỉ xử lý text: chỉ dùng được nút 4 (chat Việt → Anh)";

    public static ProviderOption From(IAiProvider p) =>
        new(p.Kind, p.DisplayName, p.DefaultModel, p.SuggestedModels, p.ApiKeyUrl, p.Capabilities);
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAiProviderRegistry _registry;
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        IAiProviderRegistry registry,
        ISettingsService settings,
        ISecretStore secrets,
        ILogger<SettingsViewModel> logger)
    {
        _registry = registry;
        _settings = settings;
        _secrets = secrets;
        _logger = logger;

        Providers = registry.All.Select(ProviderOption.From).ToList();
        _minimizeToTrayOnClose = settings.Current.MinimizeToTrayOnClose;
        SelectedProvider = Providers.First(p => p.Kind == settings.Current.Ai.ActiveProvider);
    }

    public IReadOnlyList<ProviderOption> Providers { get; }

    public ObservableCollection<string> AvailableModels { get; } = [];

    [ObservableProperty]
    private ProviderOption _selectedProvider = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private string _apiKey = "";

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private bool _isApiKeyVisible;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand), nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private StatusKind _statusKind;

    partial void OnSelectedProviderChanged(ProviderOption value)
    {
        ApiKey = _secrets.Get(SecretKeys.ApiKey(value.Kind)) ?? "";
        Model = _settings.Current.Ai.GetModel(value.Kind) ?? value.DefaultModel;
        AvailableModels.Clear();
        foreach (var model in value.SuggestedModels)
        {
            AvailableModels.Add(model);
        }
        SetStatus(StatusKind.None, null);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var kind = SelectedProvider.Kind;
        var key = ApiKey.Trim();
        if (key.Length == 0)
        {
            _secrets.Remove(SecretKeys.ApiKey(kind));
        }
        else
        {
            _secrets.Set(SecretKeys.ApiKey(kind), key);
        }

        var ai = _settings.Current.Ai;
        ai.ActiveProvider = kind;
        ai.Models[kind] = string.IsNullOrWhiteSpace(Model) ? SelectedProvider.DefaultModel : Model.Trim();
        _settings.Current.MinimizeToTrayOnClose = MinimizeToTrayOnClose;

        await _settings.SaveAsync();
        SetStatus(StatusKind.Success, $"Đã lưu. Đang dùng {SelectedProvider.DisplayName} · {ai.Models[kind]}.");
    }

    private bool CanSave() => !IsBusy;

    /// <summary>
    /// Bước 1: lấy danh sách model (kiểm tra key, không tốn token).
    /// Bước 2: gửi một prompt ngắn (kiểm tra tên model có dùng được).
    /// Kiểm tra trên giá trị đang nhập, chưa cần bấm Lưu.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        var provider = _registry.Get(SelectedProvider.Kind);
        var key = ApiKey.Trim();
        var model = string.IsNullOrWhiteSpace(Model) ? provider.DefaultModel : Model.Trim();

        try
        {
            SetStatus(StatusKind.Info, "Đang kiểm tra API key…");
            var models = await provider.ListModelsAsync(key);
            ReplaceAvailableModels(models);

            SetStatus(StatusKind.Info, $"Key hợp lệ. Đang thử model {model}…");
            var response = await provider.GenerateAsync(new AiConnection(key, model), new AiRequest
            {
                Prompt = "Reply with exactly one word: OK",
                MaxOutputTokens = 1024,
            });

            var reply = response.Text.Length > 40 ? response.Text[..40] + "…" : response.Text;
            SetStatus(StatusKind.Success,
                $"Kết nối thành công. {model} trả lời: \"{reply}\". Tìm thấy {models.Count} model khả dụng. Bấm Lưu để dùng.");
        }
        catch (AiException ex)
        {
            _logger.LogWarning(ex, "Kiểm tra kết nối {Provider} thất bại ({Kind})", provider.Kind, ex.Kind);
            SetStatus(StatusKind.Error, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong đợi khi kiểm tra {Provider}", provider.Kind);
            SetStatus(StatusKind.Error, "Lỗi không mong đợi: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanTestConnection() => !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);

    [RelayCommand]
    private void OpenApiKeyPage() =>
        Process.Start(new ProcessStartInfo(SelectedProvider.ApiKeyUrl) { UseShellExecute = true });

    private void ReplaceAvailableModels(IReadOnlyList<string> models)
    {
        if (models.Count == 0)
        {
            return;
        }
        var current = Model;
        AvailableModels.Clear();
        foreach (var m in models)
        {
            AvailableModels.Add(m);
        }
        Model = current; // ComboBox editable có thể xoá Text khi ItemsSource đổi
    }

    private void SetStatus(StatusKind kind, string? message)
    {
        StatusKind = kind;
        StatusMessage = message;
    }
}
