using System.Runtime.InteropServices;
using System.Windows;

namespace MapleHelper.App.Services;

public static class ClipboardService
{
    /// <summary>
    /// Clipboard có thể đang bị app khác khoá (CLIPBRD_E_CANT_OPEN), thử lại vài lần trước khi báo lỗi.
    /// </summary>
    public static bool TrySetText(string text)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
        return false;
    }
}
