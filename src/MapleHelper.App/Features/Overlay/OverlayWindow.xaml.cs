using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MapleHelper.App.Services;
using MapleHelper.Core.Settings;
using MapleHelper.Native;

namespace MapleHelper.App.Features.Overlay;

/// <summary>
/// Nút nổi trên game. Không lấy focus khỏi game (WS_EX_NOACTIVATE), vùng trong suốt cho click xuyên qua.
/// Menu bung về phía còn chỗ trống: mặc định sang trái, sát mép trái màn hình thì sang phải.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double MainSize = 76;
    private const double SubSize = 52;
    private const double DragThreshold = 4;

    // Vị trí 4 nút phụ so với tâm nút chính khi bung sang trái (theo phác thảo: 1 trên, 2-3 bên, 4 dưới)
    private static readonly Vector[] SubOffsets = [new(-28, -100), new(-98, -42), new(-98, 42), new(-28, 100)];

    private readonly Button[] _subButtons;
    private readonly DispatcherTimer _topmostTimer;
    private Point _mouseDownPosition;
    private bool _isMouseDown;
    private bool _isDragging;
    private bool _menuOnLeft = true;

    public OverlayWindow()
    {
        InitializeComponent();
        _subButtons = [Sub1, Sub2, Sub3, Sub4];
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) => WindowStyles.BringToTopmost(Handle);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _topmostTimer.Start(); else _topmostTimer.Stop();
        };
        LayoutButtons();
    }

    public event EventHandler<HotkeyAction>? ActionRequested;
    public event EventHandler? OpenMainWindowRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;

    /// <summary>Phát khi người dùng kéo xong, để lưu vị trí.</summary>
    public event EventHandler<Point>? MainButtonMoved;

    public bool IsMenuOpen { get; private set; }

    public nint Handle => new WindowInteropHelper(this).Handle;

    /// <summary>Tâm nút chính theo toạ độ màn hình (DIP).</summary>
    public Point MainCenter => new(Left + MainButtonCenter.X, Top + MainButtonCenter.Y);

    private Point MainButtonCenter => _menuOnLeft ? new(Width - 54, Height / 2) : new(54, Height / 2);

    public void UpdateTooltips(IReadOnlyDictionary<HotkeyAction, string> hotkeyTexts)
    {
        foreach (var button in _subButtons)
        {
            var action = Enum.Parse<HotkeyAction>((string)button.Tag);
            var text = HotkeyText.ActionName(action);
            if (hotkeyTexts.TryGetValue(action, out var keys) && keys.Length > 0)
            {
                text += $"   ({keys})";
            }
            button.ToolTip = text;
        }
    }

    /// <summary>Đặt overlay sao cho tâm nút chính nằm ở <paramref name="center"/>.</summary>
    public void PlaceMainCenter(Point center)
    {
        // Bung sang trái nếu bên trái còn đủ chỗ cho menu
        _menuOnLeft = center.X - 140 >= SystemParameters.VirtualScreenLeft;
        LayoutButtons();
        Left = center.X - MainButtonCenter.X;
        Top = center.Y - MainButtonCenter.Y;
    }

    public void ToggleMenu()
    {
        if (IsMenuOpen) CloseMenu(); else OpenMenu();
    }

    public void OpenMenu()
    {
        IsMenuOpen = true;
        var direction = _menuOnLeft ? 1 : -1;
        for (var i = 0; i < _subButtons.Length; i++)
        {
            var button = _subButtons[i];
            var transform = (TranslateTransform)button.RenderTransform;
            var delay = TimeSpan.FromMilliseconds(35 * i);
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
            transform.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(SubOffsets[i].X * direction, TimeSpan.FromMilliseconds(260)) { BeginTime = delay, EasingFunction = ease });
            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(SubOffsets[i].Y, TimeSpan.FromMilliseconds(260)) { BeginTime = delay, EasingFunction = ease });
            button.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { BeginTime = delay });
            button.IsHitTestVisible = true;
        }
    }

    public void CloseMenu()
    {
        IsMenuOpen = false;
        foreach (var button in _subButtons)
        {
            var transform = (TranslateTransform)button.RenderTransform;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            button.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
            button.IsHitTestVisible = false;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.MakeOverlay(Handle, noActivate: true);
    }

    private void LayoutButtons()
    {
        var center = MainButtonCenter;
        Canvas.SetLeft(MainButton, center.X - MainSize / 2);
        Canvas.SetTop(MainButton, center.Y - MainSize / 2);
        foreach (var button in _subButtons)
        {
            Canvas.SetLeft(button, center.X - SubSize / 2);
            Canvas.SetTop(button, center.Y - SubSize / 2);
            if (button.RenderTransform is not TranslateTransform)
            {
                button.RenderTransform = new TranslateTransform();
            }
        }
    }

    private void OnSubButtonClick(object sender, RoutedEventArgs e)
    {
        var action = Enum.Parse<HotkeyAction>((string)((Button)sender).Tag);
        CloseMenu();
        ActionRequested?.Invoke(this, action);
    }

    private void OnMainMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isMouseDown = true;
        _isDragging = false;
        _mouseDownPosition = e.GetPosition(this);
        MainButton.CaptureMouse();
        e.Handled = true;
    }

    private void OnMainMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDown)
        {
            return;
        }
        // Vị trí chuột so với cửa sổ giữ nguyên khi cửa sổ đi theo, nên delta luôn tính từ điểm bấm ban đầu
        var delta = e.GetPosition(this) - _mouseDownPosition;
        if (!_isDragging && delta.Length < DragThreshold)
        {
            return;
        }
        if (!_isDragging)
        {
            _isDragging = true;
            CloseMenu();
        }
        Left += delta.X;
        Top += delta.Y;
    }

    private void OnMainMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown)
        {
            return;
        }
        _isMouseDown = false;
        MainButton.ReleaseMouseCapture();

        if (_isDragging)
        {
            _isDragging = false;
            var center = MainCenter;
            PlaceMainCenter(center); // đổi hướng bung nếu vừa kéo sát mép
            MainButtonMoved?.Invoke(this, center);
        }
        else
        {
            ToggleMenu();
        }
        e.Handled = true;
    }

    private void OnOpenMainWindow(object sender, RoutedEventArgs e) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);

    private void OnHideOverlay(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this, EventArgs.Empty);

    private void OnExit(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
