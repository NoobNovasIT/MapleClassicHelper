using System.Windows.Input;
using MapleHelper.Native;

namespace MapleHelper.App.Features.Snipping;

public sealed class ScreenCaptureBlockedException()
    : Exception("Ảnh chụp toàn màu đen. Game có thể đang ở chế độ Full Screen độc quyền: hãy chọn độ phân giải không có chữ \"Full Screen\" trong Options → Graphics.");

public sealed class SnippingService
{
    private bool _isRunning;

    /// <summary>
    /// Chụp đóng băng tất cả màn hình, cho người dùng kéo chọn một vùng. Trả về null nếu huỷ.
    /// Người gọi phải ẩn overlay/popup của app trước khi gọi để chúng không lọt vào ảnh.
    /// </summary>
    public async Task<SnipResult?> SelectRegionAsync(string hint)
    {
        if (_isRunning)
        {
            return null;
        }
        _isRunning = true;
        try
        {
            var shots = ScreenCapture.GetMonitors()
                .Select(m => (Monitor: m, Image: ScreenCapture.Capture(m.Bounds)))
                .ToList();
            if (shots.All(s => ScreenCapture.IsMostlyBlack(s.Image)))
            {
                throw new ScreenCaptureBlockedException();
            }

            var completion = new TaskCompletionSource<SnipResult?>();
            var windows = shots.Select(s => new SnipWindow(s.Monitor, s.Image, hint)).ToList();
            foreach (var window in windows)
            {
                window.Completed += (_, result) => completion.TrySetResult(result);
                window.Show();
            }

            // Màn hình đang có chuột nhận focus bàn phím (để Esc hoạt động)
            var (cx, cy) = ScreenCapture.CursorPosition();
            var active = windows.FirstOrDefault(w => w.Monitor.Bounds.Contains(cx, cy)) ?? windows[0];
            active.Activate();
            Keyboard.Focus(active);

            var result = await completion.Task;
            foreach (var window in windows)
            {
                window.Close();
            }
            return result;
        }
        finally
        {
            _isRunning = false;
        }
    }
}
