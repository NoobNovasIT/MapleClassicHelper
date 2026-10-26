using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MapleHelper.Native;

namespace MapleHelper.App.Features.Snipping;

/// <summary>
/// Phủ một màn hình bằng ảnh chụp sẵn của chính màn hình đó. Toạ độ chuột (DIP) được quy đổi sang
/// pixel của ảnh theo tỉ lệ kích thước, nên đúng với mọi mức scale DPI.
/// </summary>
public partial class SnipWindow : Window
{
    private const double MinSelection = 6;

    private readonly MonitorInfo _monitor;
    private readonly BitmapSource _screenshot;
    private Point? _start;

    public SnipWindow(MonitorInfo monitor, BitmapSource screenshot, string hint)
    {
        InitializeComponent();
        _monitor = monitor;
        _screenshot = screenshot;
        Frozen.Source = screenshot;
        HintText.Text = hint + "   ·   Esc hoặc chuột phải để huỷ";
        SizeChanged += (_, _) => FullRect.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
    }

    public MonitorInfo Monitor => _monitor;

    /// <summary>Phát khi người dùng chọn xong (kết quả) hoặc huỷ (null).</summary>
    public event EventHandler<SnipResult?>? Completed;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeOverlay(handle, noActivate: false);
        var b = _monitor.Bounds;
        WindowStyles.SetPhysicalBounds(handle, b.X, b.Y, b.Width, b.Height);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Completed?.Invoke(this, null);
        }
        base.OnKeyDown(e);
    }

    private void OnCancel(object sender, MouseButtonEventArgs e) => Completed?.Invoke(this, null);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        Root.CaptureMouse();
        Hint.Visibility = Visibility.Collapsed;
        UpdateSelection(_start.Value);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_start is not null)
        {
            UpdateSelection(e.GetPosition(Root));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null)
        {
            return;
        }
        Root.ReleaseMouseCapture();
        var rect = new Rect(_start.Value, e.GetPosition(Root));
        _start = null;

        if (rect.Width < MinSelection || rect.Height < MinSelection)
        {
            // Lỡ bấm nhầm: cho chọn lại
            Selection.Visibility = SizeBadge.Visibility = Visibility.Collapsed;
            HoleRect.Rect = Rect.Empty;
            Hint.Visibility = Visibility.Visible;
            return;
        }
        Completed?.Invoke(this, Crop(rect));
    }

    private void UpdateSelection(Point current)
    {
        var rect = new Rect(_start!.Value, current);
        HoleRect.Rect = rect;
        System.Windows.Controls.Canvas.SetLeft(Selection, rect.X);
        System.Windows.Controls.Canvas.SetTop(Selection, rect.Y);
        Selection.Width = rect.Width;
        Selection.Height = rect.Height;
        Selection.Visibility = Visibility.Visible;

        var pixels = ToPixels(rect);
        SizeText.Text = $"{pixels.Width} × {pixels.Height}";
        System.Windows.Controls.Canvas.SetLeft(SizeBadge, rect.X);
        System.Windows.Controls.Canvas.SetTop(SizeBadge, Math.Max(0, rect.Y - 24));
        SizeBadge.Visibility = Visibility.Visible;
    }

    private Int32Rect ToPixels(Rect dip)
    {
        var scaleX = _screenshot.PixelWidth / Math.Max(1, Root.ActualWidth);
        var scaleY = _screenshot.PixelHeight / Math.Max(1, Root.ActualHeight);
        var x = (int)Math.Clamp(Math.Round(dip.X * scaleX), 0, _screenshot.PixelWidth - 1);
        var y = (int)Math.Clamp(Math.Round(dip.Y * scaleY), 0, _screenshot.PixelHeight - 1);
        var width = (int)Math.Clamp(Math.Round(dip.Width * scaleX), 1, _screenshot.PixelWidth - x);
        var height = (int)Math.Clamp(Math.Round(dip.Height * scaleY), 1, _screenshot.PixelHeight - y);
        return new Int32Rect(x, y, width, height);
    }

    private SnipResult Crop(Rect dip)
    {
        var pixels = ToPixels(dip);
        var cropped = new CroppedBitmap(_screenshot, pixels);
        cropped.Freeze();

        var physical = new PixelRect(_monitor.Bounds.X + pixels.X, _monitor.Bounds.Y + pixels.Y, pixels.Width, pixels.Height);
        var screenDip = new Rect(Left + dip.X, Top + dip.Y, dip.Width, dip.Height);
        return new SnipResult(cropped, physical, screenDip);
    }
}

/// <param name="ScreenDip">Vùng đã chọn theo DIP trên màn hình, dùng để đặt popup kết quả bên cạnh.</param>
public sealed record SnipResult(BitmapSource Image, PixelRect Physical, Rect ScreenDip);
