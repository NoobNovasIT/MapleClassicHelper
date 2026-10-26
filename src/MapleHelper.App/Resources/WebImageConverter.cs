using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MapleHelper.App.Resources;

/// <summary>
/// URL ảnh → BitmapImage giải mã ở độ rộng nhỏ (ảnh tin Nexon 540px) để danh sách dài không tốn RAM.
/// WPF tự tải ảnh nền, không chặn UI.
/// </summary>
public sealed class WebImageConverter : IValueConverter
{
    private static readonly Dictionary<string, BitmapImage> Cache = [];

    public int DecodeWidth { get; set; } = 320;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var key = $"{DecodeWidth}|{url}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.DecodePixelWidth = DecodeWidth;
        image.CacheOption = BitmapCacheOption.OnDemand;
        image.EndInit();
        Cache[key] = image;
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
