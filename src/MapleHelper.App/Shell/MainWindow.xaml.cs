using System.ComponentModel;
using System.Windows;
using MapleHelper.App.Services;
using MapleHelper.Core.Settings;

namespace MapleHelper.App.Shell;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly TrayIconService _tray;
    private bool _trayHintShown;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, TrayIconService tray)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        _tray = tray;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.Current.IsExiting)
        {
            base.OnClosing(e);
            return;
        }

        if (_settings.Current.MinimizeToTrayOnClose)
        {
            // Ẩn xuống khay để hotkey và overlay vẫn hoạt động
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowNotification("Maple Classic Helper vẫn đang chạy",
                    "Nhấp đúp icon ở khay hệ thống để mở lại. Chuột phải để thoát.");
            }
            return;
        }

        App.Current.ExitApplication();
    }
}
