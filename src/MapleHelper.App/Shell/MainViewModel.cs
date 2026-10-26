using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Features.Placeholder;
using MapleHelper.App.Features.Settings;

namespace MapleHelper.App.Shell;

public sealed record NavItem(string Title, string Glyph, object Page);

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(SettingsViewModel settings)
    {
        NavItems =
        [
            new("Điểm tin", "", new PlaceholderViewModel("Điểm tin",
                "Tin tức, sự kiện, bảo trì của Maple Classic World được dịch sang tiếng Việt.", "M3")),
            new("Đã lưu", "", new PlaceholderViewModel("Đã lưu",
                "Các bản tin và kết quả tra cứu bạn đã ghim.", "M3")),
            new("Lịch sử tra cứu", "", new PlaceholderViewModel("Lịch sử tra cứu",
                "Lịch sử dịch, tìm item và hướng dẫn quest.", "M4")),
            new("Cài đặt", "", settings),
        ];
        SelectedNav = NavItems[^1];
    }

    public IReadOnlyList<NavItem> NavItems { get; }

    public string OverlayHotkeyText => "Hotkey: Ctrl + Shift + M";

    [ObservableProperty]
    private NavItem? _selectedNav;

    [RelayCommand]
    private static void StartOverlay() =>
        MessageBox.Show("Overlay mode sẽ có ở mốc M1.", "Maple Classic Helper",
            MessageBoxButton.OK, MessageBoxImage.Information);
}
