using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Features.History;
using MapleHelper.App.Features.News;
using MapleHelper.App.Features.Overlay;
using MapleHelper.App.Features.Saved;
using MapleHelper.App.Features.Settings;
using MapleHelper.App.Services;
using MapleHelper.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.App.Shell;

public sealed record NavItem(string Title, string Glyph, object Page);

public sealed partial class MainViewModel : ObservableObject
{
    private readonly OverlayCoordinator _overlay;
    private readonly ISettingsService _settings;
    private readonly NavItem _newsNav;

    public MainViewModel(
        NewsViewModel news,
        SavedViewModel saved,
        HistoryViewModel history,
        SettingsViewModel settingsPage,
        OverlayCoordinator overlay,
        ISettingsService settings)
    {
        _overlay = overlay;
        _settings = settings;
        _newsNav = new("Điểm tin", "", news);
        NavItems =
        [
            _newsNav,
            new("Đã lưu", "", saved),
            new("Lịch sử tra cứu", "", history),
            new("Cài đặt", "", settingsPage),
        ];
        _selectedNav = _newsNav;

        saved.OpenNewsRequested += async (_, item) =>
        {
            SelectedNav = _newsNav;
            await news.OpenCommand.ExecuteAsync(item);
        };
        settings.Changed += (_, _) => OnPropertyChanged(nameof(OverlayHotkeyText));
        overlay.OverlayVisibilityChanged += (_, _) => OnPropertyChanged(nameof(OverlayButtonText));
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    [ObservableProperty]
    private NavItem _selectedNav;

    public string OverlayButtonText => _overlay.IsOverlayVisible ? "Tắt overlay" : "Overlay mode";

    public string OverlayHotkeyText
    {
        get
        {
            var binding = _settings.Current.Hotkeys.FirstOrDefault(h => h.Action == HotkeyAction.ToggleOverlay && h.Enabled);
            return binding is null || binding.VirtualKey == 0 ? "Chưa gán hotkey" : "Hotkey: " + HotkeyText.Format(binding);
        }
    }

    public void ShowSettings() => SelectedNav = NavItems[^1];

    /// <summary>Bật overlay và thu cửa sổ chính xuống khay để người dùng vào game.</summary>
    [RelayCommand]
    private void StartOverlay()
    {
        if (_overlay.IsOverlayVisible)
        {
            _overlay.HideOverlay();
            return;
        }
        _overlay.ShowOverlay();
        App.Current.Services.GetRequiredService<MainWindow>().Hide();
    }
}
