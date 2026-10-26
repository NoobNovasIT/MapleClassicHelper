using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.Core.Ai;
using MapleHelper.Core.Settings;
using MapleHelper.Core.Updates;
using MapleHelper.Infrastructure;
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
        ? "Đọc ảnh, tìm kiếm web: dùng được cho nút 2 (tìm item) và nút 3 (quest)"
        : "Chỉ xử lý text: không đọc được ảnh nên không dùng được cho nút 2 và nút 3";

    public static ProviderOption From(IAiProvider p) =>
        new(p.Kind, p.DisplayName, p.DefaultModel, p.SuggestedModels, p.ApiKeyUrl, p.Capabilities);
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAiProviderRegistry _registry;
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IUpdateService _updates;
    private readonly AppPaths _paths;
    private readonly bool _initialized;

    public SettingsViewModel(
        IAiProviderRegistry registry,
        ISettingsService settings,
        ISecretStore secrets,
        HotkeySettingsViewModel hotkeys,
        TranslatorSettingsViewModel translator,
        IUpdateService updates,
        AppPaths paths,
        ILogger<SettingsViewModel> logger)
    {
        _registry = registry;
        _settings = settings;
        _secrets = secrets;
        _updates = updates;
        _paths = paths;
        _logger = logger;
        Hotkeys = hotkeys;
        Translator = translator;

        Providers = registry.All.Select(ProviderOption.From).ToList();
        SelectedProvider = Providers.First(p => p.Kind == settings.Current.Ai.ActiveProvider);

        var s = settings.Current;
        _minimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        _showOverlayOnStartup = s.Overlay.ShowOnStartup;
        _useAiForScreenTranslate = s.Overlay.ScreenTranslateEngine == ScreenTranslateEngine.Ai;
        _classicOnly = s.News.ClassicOnly;
        _notifyNewArticles = s.News.NotifyNewArticles;
        _repositoryUrl = s.Updates.RepositoryUrl;
        _checkUpdatesOnStartup = s.Updates.CheckOnStartup;
        _initialized = true;
    }

    public HotkeySettingsViewModel Hotkeys { get; }

    public TranslatorSettingsViewModel Translator { get; }

    public string Version => _updates.CurrentVersion;

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

    // Tuỳ chọn chung: lưu ngay khi đổi
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _showOverlayOnStartup;
    [ObservableProperty] private bool _useAiForScreenTranslate;
    [ObservableProperty] private bool _classicOnly;
    [ObservableProperty] private bool _notifyNewArticles;
    [ObservableProperty] private bool _checkUpdatesOnStartup;

    // Cập nhật
    [ObservableProperty] private string _repositoryUrl;
    [ObservableProperty] private string? _updateStatus;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private int _updateProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand), nameof(InstallUpdateCommand))]
    private bool _isUpdating;

    partial void OnMinimizeToTrayOnCloseChanged(bool value) => SaveGeneral(s => s.MinimizeToTrayOnClose = value);
    partial void OnShowOverlayOnStartupChanged(bool value) => SaveGeneral(s => s.Overlay.ShowOnStartup = value);
    partial void OnUseAiForScreenTranslateChanged(bool value) =>
        SaveGeneral(s => s.Overlay.ScreenTranslateEngine = value ? ScreenTranslateEngine.Ai : ScreenTranslateEngine.Google);
    partial void OnClassicOnlyChanged(bool value) => SaveGeneral(s => s.News.ClassicOnly = value);
    partial void OnNotifyNewArticlesChanged(bool value) => SaveGeneral(s => s.News.NotifyNewArticles = value);
    partial void OnCheckUpdatesOnStartupChanged(bool value) => SaveGeneral(s => s.Updates.CheckOnStartup = value);

    private async void SaveGeneral(Action<AppSettings> apply)
    {
        if (!_initialized)
        {
            return;
        }
        apply(_settings.Current);
        await _settings.SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdatesAsync()
    {
        IsUpdating = true;
        try
        {
            _settings.Current.Updates.RepositoryUrl = RepositoryUrl.Trim();
            await _settings.SaveAsync();
            UpdateStatus = "Đang kiểm tra…";
            var result = await _updates.CheckAsync();
            UpdateStatus = result.Message;
            IsUpdateAvailable = result.Status == Core.Updates.UpdateStatus.Available;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private bool CanCheckUpdates() => !IsUpdating;

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task InstallUpdateAsync()
    {
        IsUpdating = true;
        try
        {
            UpdateStatus = "Đang tải bản cập nhật…";
            await _updates.DownloadAndRestartAsync(new Progress<int>(p => UpdateProgress = p));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cài bản cập nhật thất bại");
            UpdateStatus = "Cập nhật thất bại: " + ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => Process.Start(new ProcessStartInfo(_paths.Root) { UseShellExecute = true });

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
