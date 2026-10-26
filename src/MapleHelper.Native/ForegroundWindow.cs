using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MapleHelper.Native;

/// <summary>
/// Windows chặn process nền tự đưa cửa sổ lên trước. Instance thứ hai gọi <see cref="AllowAnyProcess"/>
/// trước khi báo cho instance đang chạy, để instance đó được phép <see cref="BringToFront"/>.
/// </summary>
public static class ForegroundWindow
{
    public static void AllowAnyProcess() => PInvoke.AllowSetForegroundWindow(PInvoke.ASFW_ANY);

    public static nint Current => PInvoke.GetForegroundWindow();

    public static void BringToFront(nint handle)
    {
        var hwnd = (HWND)handle;
        if (handle == 0 || !PInvoke.IsWindow(hwnd))
        {
            return;
        }
        if (PInvoke.IsIconic(hwnd))
        {
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        }
        PInvoke.SetForegroundWindow(hwnd);
    }
}
