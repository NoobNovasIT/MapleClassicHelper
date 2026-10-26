using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace MapleHelper.App.Services;

public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;

    public void Initialize(Action showMainWindow, Action exit)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Mở Maple Classic Helper", showMainWindow));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Thoát", exit));

        _icon = new TaskbarIcon
        {
            ToolTipText = "Maple Classic Helper",
            ContextMenu = menu,
            // Icon tạm sinh từ chữ, thay bằng icon Maple khi có file .ico
            IconSource = new GeneratedIconSource
            {
                Text = "M",
                Foreground = Brushes.White,
                Background = (Brush)Application.Current.FindResource("Brush.Accent"),
                FontWeight = FontWeights.Bold,
            },
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
