using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace MapleHelper.Native;

/// <summary>Vùng màn hình theo pixel vật lý (không phải DIP của WPF).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

public sealed record MonitorInfo(PixelRect Bounds, bool IsPrimary);

public static class ScreenCapture
{
    /// <summary>Vị trí chuột theo pixel vật lý.</summary>
    public static (int X, int Y) CursorPosition()
    {
        PInvoke.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    public static unsafe IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (PInvoke.GetMonitorInfo(hMonitor, ref info))
            {
                var r = info.rcMonitor;
                monitors.Add(new MonitorInfo(
                    new PixelRect(r.left, r.top, r.right - r.left, r.bottom - r.top),
                    (info.dwFlags & 1) != 0)); // MONITORINFOF_PRIMARY
            }
            return true;
        }, 0);
        return monitors;
    }

    /// <summary>
    /// Chụp một vùng bằng GDI BitBlt. Chạy được với game ở chế độ cửa sổ/borderless.
    /// Game ở exclusive fullscreen thường trả về ảnh đen, xem <see cref="IsMostlyBlack"/>.
    /// </summary>
    public static unsafe BitmapSource Capture(PixelRect area)
    {
        var screenDc = PInvoke.GetDC(HWND.Null);
        var memoryDc = PInvoke.CreateCompatibleDC(screenDc);
        var bitmap = PInvoke.CreateCompatibleBitmap(screenDc, area.Width, area.Height);
        try
        {
            var previous = PInvoke.SelectObject(memoryDc, bitmap);
            PInvoke.BitBlt(memoryDc, 0, 0, area.Width, area.Height, screenDc, area.X, area.Y,
                ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT);
            PInvoke.SelectObject(memoryDc, previous);

            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = area.Width;
            info.bmiHeader.biHeight = -area.Height; // âm = top-down, khớp thứ tự dòng của WPF
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;

            var stride = area.Width * 4;
            var pixels = new byte[stride * area.Height];
            fixed (byte* p = pixels)
            {
                PInvoke.GetDIBits(memoryDc, bitmap, 0, (uint)area.Height, p, &info, DIB_USAGE.DIB_RGB_COLORS);
            }

            var source = BitmapSource.Create(area.Width, area.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
            source.Freeze();
            return source;
        }
        finally
        {
            PInvoke.DeleteObject(bitmap);
            PInvoke.DeleteDC(memoryDc);
            _ = PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }

    /// <summary>Gần như toàn bộ pixel là đen: dấu hiệu game đang ở exclusive fullscreen.</summary>
    public static bool IsMostlyBlack(BitmapSource image)
    {
        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);

        long dark = 0, total = 0;
        for (var i = 0; i < pixels.Length; i += 4 * 7) // lấy mẫu thưa cho nhanh
        {
            total++;
            if (pixels[i] < 8 && pixels[i + 1] < 8 && pixels[i + 2] < 8)
            {
                dark++;
            }
        }
        return total > 0 && dark > total * 0.98;
    }

    public static byte[] ToPng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
