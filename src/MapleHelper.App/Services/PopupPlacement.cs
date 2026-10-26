using System.Windows;

namespace MapleHelper.App.Services;

public static class PopupPlacement
{
    /// <summary>
    /// Đặt popup cạnh vùng <paramref name="anchor"/> (DIP): ưu tiên bên phải, hết chỗ thì bên trái,
    /// cuối cùng là ngay dưới. Luôn kẹp trong màn hình ảo.
    /// </summary>
    public static void PlaceBeside(Window popup, Rect anchor, double gap = 8)
    {
        var width = popup.Width;
        var height = popup.ActualHeight > 0 ? popup.ActualHeight : (double.IsNaN(popup.Height) ? 300 : popup.Height);
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        double left;
        double top = anchor.Top;
        if (anchor.Right + gap + width <= screen.Right)
        {
            left = anchor.Right + gap;
        }
        else if (anchor.Left - gap - width >= screen.Left)
        {
            left = anchor.Left - gap - width;
        }
        else
        {
            left = anchor.Left;
            top = anchor.Bottom + gap;
        }

        popup.Left = Math.Clamp(left, screen.Left, Math.Max(screen.Left, screen.Right - width));
        popup.Top = Math.Clamp(top, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
    }

    /// <summary>Góc dưới bên trái vùng làm việc, gần ô chat của MapleStory.</summary>
    public static void PlaceNearGameChat(Window popup)
    {
        var area = SystemParameters.WorkArea;
        popup.Left = area.Left + 24;
        popup.Top = area.Bottom - (double.IsNaN(popup.Height) ? 260 : popup.Height) - 140;
    }
}
