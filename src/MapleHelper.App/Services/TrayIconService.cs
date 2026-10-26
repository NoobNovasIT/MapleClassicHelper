using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace MapleHelper.App.Services;

public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;

    public void Initialize(Action showMainWindow, Action exit, Action toggleOverlay)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Mở Maple Classic Helper", showMainWindow));
        menu.Items.Add(CreateMenuItem("Bật / tắt overlay", toggleOverlay));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Thoát", exit));

        _icon = new TaskbarIcon
        {
            ToolTipText = "Maple Classic Helper",
            ContextMenu = menu,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/Icons/app.ico")),
        };
        _icon.TrayMouseDoubleClick += (_, _) => showMainWindow();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void ShowNotification(string title, string message) =>
        _icon?.ShowNotification(title, message, NotificationIcon.Info);

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
