using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.Settings;
using MapleHelper.Infrastructure.Translation;

namespace MapleHelper.App.Features.Settings;

/// <summary>Key Azure Translator, dùng khi Google Dịch bị chặn.</summary>
public sealed partial class TranslatorSettingsViewModel : ObservableObject
{
    private const string PortalUrl = "https://portal.azure.com/#create/Microsoft.CognitiveServicesTextTranslation";

    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly AzureTranslatorClient _azure;

    public TranslatorSettingsViewModel(ISettingsService settings, ISecretStore secrets, AzureTranslatorClient azure)
    {
        _settings = settings;
        _secrets = secrets;
        _azure = azure;
        _azureKey = secrets.Get(SecretKeys.AzureTranslatorKey) ?? "";
        _azureRegion = settings.Current.Translator.AzureRegion;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private string _azureKey;

    [ObservableProperty] private string _azureRegion;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _isError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand), nameof(SaveCommand))]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveAsync()
    {
        var key = AzureKey.Trim();
        if (key.Length == 0)
        {
            _secrets.Remove(SecretKeys.AzureTranslatorKey);
        }
        else
        {
            _secrets.Set(SecretKeys.AzureTranslatorKey, key);
        }
        _settings.Current.Translator.AzureRegion = AzureRegion.Trim();
        await _settings.SaveAsync();

        IsError = false;
        Status = key.Length == 0 ? "Đã xoá key. Khi Google bị chặn, app sẽ báo lỗi." : "Đã lưu. App sẽ tự chuyển sang Azure khi Google bị chặn.";
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        IsBusy = true;
        Status = "Đang thử dịch…";
        try
        {
            var result = await _azure.TranslateWithKeyAsync(AzureKey, AzureRegion, "cho tôi vào tổ đội với", "vi", "en");
            IsError = false;
            Status = $"Kết nối thành công: \"cho tôi vào tổ đội với\" → \"{result}\". Bấm Lưu để dùng.";
        }
        catch (MachineTranslationException ex)
        {
            IsError = true;
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRun() => !IsBusy;

    private bool CanTest() => !IsBusy && !string.IsNullOrWhiteSpace(AzureKey);

    [RelayCommand]
    private static void OpenPortal() => Process.Start(new ProcessStartInfo(PortalUrl) { UseShellExecute = true });
}
