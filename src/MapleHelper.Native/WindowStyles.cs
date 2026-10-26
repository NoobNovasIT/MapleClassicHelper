using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MapleHelper.Native;

public static class WindowStyles
{
    /// <summary>
    /// Kiểu cửa sổ cho overlay: không hiện trên Alt+Tab (TOOLWINDOW), bấm vào không lấy focus
    /// khỏi game (NOACTIVATE) và luôn nằm trên cùng.
    /// </summary>
    public static void MakeOverlay(nint handle, bool noActivate)
    {
        var hwnd = (HWND)handle;
        var style = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        style |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST;
        style &= ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        if (noActivate)
        {
            style |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        }
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)style);
        BringToTopmost(handle);
    }

    /// <summary>Game borderless đôi khi tự đẩy mình lên trên, gọi định kỳ để overlay giữ vị trí trên cùng.</summary>
    public static void BringToTopmost(nint handle) =>
        PInvoke.SetWindowPos((HWND)handle, HWND.HWND_TOPMOST, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    /// <summary>Đặt cửa sổ theo pixel vật lý, tránh sai lệch khi mỗi màn hình có DPI khác nhau.</summary>
    public static void SetPhysicalBounds(nint handle, int x, int y, int width, int height) =>
        PInvoke.SetWindowPos((HWND)handle, HWND.HWND_TOPMOST, x, y, width, height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
}
