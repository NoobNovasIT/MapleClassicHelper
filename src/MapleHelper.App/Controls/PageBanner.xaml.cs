using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MapleHelper.App.Controls;

/// <summary>Banner trời xanh kiểu trang tin MapleStory, dùng làm đầu trang.</summary>
public partial class PageBanner : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageBanner), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageBanner), new PropertyMetadata(""));

    public PageBanner()
    {
        InitializeComponent();
        SizeChanged += (_, _) => StartWalking();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    private void StartWalking()
    {
        if (ActualWidth <= 0)
        {
            return;
        }
        // Ốc sên đi chậm (khoảng 25 px/giây) từ trái sang phải rồi lặp lại
        var distance = ActualWidth + 60;
        var walk = new DoubleAnimation(-50, distance - 50, TimeSpan.FromSeconds(distance / 25))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        WalkerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, walk);
    }
}
