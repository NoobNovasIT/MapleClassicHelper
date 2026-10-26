using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapleHelper.App.Controls;

/// <summary>
/// Đọc frame sprite đã nhúng trong Assets/Sprites. Tự liệt kê frame từ bảng resource của assembly
/// nên thêm frame mới chỉ cần chạy lại tools/fetch-assets.ps1.
/// </summary>
public static class SpriteLibrary
{
    private static readonly Lazy<IReadOnlyList<string>> ResourceKeys = new(LoadResourceKeys);
    private static readonly Dictionary<string, IReadOnlyList<ImageSource>> Cache = [];

    /// <summary>name dạng "mobs/orange-mushroom" hoặc "npcs/maple-administrator".</summary>
    public static IReadOnlyList<ImageSource> GetFrames(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var prefix = $"assets/sprites/{name.ToLowerInvariant()}/";
            var single = $"assets/sprites/{name.ToLowerInvariant()}.png";
            var keys = ResourceKeys.Value
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.EndsWith(".png", StringComparison.Ordinal))
                .OrderBy(k => int.TryParse(Path.GetFileNameWithoutExtension(k), out var n) ? n : 0)
                .ToList();
            if (keys.Count == 0 && ResourceKeys.Value.Contains(single))
            {
                keys.Add(single);
            }

            IReadOnlyList<ImageSource> frames = keys.Select(Load).ToList();
            Cache[name] = frames;
            return frames;
        }
    }

    public static ImageSource Load(string resourcePath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/" + resourcePath.TrimStart('/'), UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static IReadOnlyList<string> LoadResourceKeys()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetName().Name + ".g.resources";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return [];
        }
        using var reader = new ResourceReader(stream);
        return reader.Cast<DictionaryEntry>()
            .Select(e => ((string)e.Key).ToLower(CultureInfo.InvariantCulture))
            .ToList();
    }
}
