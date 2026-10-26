using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MapleHelper.App.Controls;

/// <summary>
/// Chạy lần lượt các frame của một sprite Maple (Assets/Sprites/...). Frame có kích thước khác nhau
/// nên neo theo chân (giữa, dưới) để con quái không bị nhảy lên xuống.
/// </summary>
public sealed class AnimatedSprite : FrameworkElement
{
    public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
        nameof(Sprite), typeof(string), typeof(AnimatedSprite),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnSpriteChanged));

    public static readonly DependencyProperty FrameIntervalProperty = DependencyProperty.Register(
        nameof(FrameInterval), typeof(TimeSpan), typeof(AnimatedSprite),
        new PropertyMetadata(TimeSpan.FromMilliseconds(180), OnIntervalChanged));

    public static readonly DependencyProperty FlipProperty = DependencyProperty.Register(
        nameof(Flip), typeof(bool), typeof(AnimatedSprite),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(double), typeof(AnimatedSprite),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private readonly DispatcherTimer _timer;
    private IReadOnlyList<ImageSource> _frames = [];
    private int _index;

    public AnimatedSprite()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = FrameInterval };
        _timer.Tick += (_, _) => NextFrame();
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _timer.Stop();
        IsVisibleChanged += (_, _) => UpdateTimer();
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    /// <summary>Ví dụ "mobs/orange-mushroom".</summary>
    public string? Sprite
    {
        get => (string?)GetValue(SpriteProperty);
        set => SetValue(SpriteProperty, value);
    }

    public TimeSpan FrameInterval
    {
        get => (TimeSpan)GetValue(FrameIntervalProperty);
        set => SetValue(FrameIntervalProperty, value);
    }

    /// <summary>Sprite gốc quay mặt sang trái; Flip = true để quay sang phải.</summary>
    public bool Flip
    {
        get => (bool)GetValue(FlipProperty);
        set => SetValue(FlipProperty, value);
    }

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_frames.Count == 0)
        {
            return default;
        }
        var width = _frames.Max(f => f.Width) * Scale;
        var height = _frames.Max(f => f.Height) * Scale;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_frames.Count == 0)
        {
            return;
        }
        var frame = _frames[_index % _frames.Count];
        var width = frame.Width * Scale;
        var height = frame.Height * Scale;
        var x = (ActualWidth - width) / 2;
        var y = ActualHeight - height;

        if (Flip)
        {
            dc.PushTransform(new ScaleTransform(-1, 1, ActualWidth / 2, 0));
        }
        dc.DrawImage(frame, new Rect(x, y, width, height));
        if (Flip)
        {
            dc.Pop();
        }
    }

    private void NextFrame()
    {
        _index = (_index + 1) % Math.Max(1, _frames.Count);
        InvalidateVisual();
    }

    private void UpdateTimer()
    {
        if (IsLoaded && IsVisible && _frames.Count > 1)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private static void OnSpriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sprite = (AnimatedSprite)d;
        sprite._frames = e.NewValue is string name ? SpriteLibrary.GetFrames(name) : [];
        sprite._index = 0;
        sprite.InvalidateVisual();
        sprite.UpdateTimer();
    }

    private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AnimatedSprite)d)._timer.Interval = (TimeSpan)e.NewValue;
}
