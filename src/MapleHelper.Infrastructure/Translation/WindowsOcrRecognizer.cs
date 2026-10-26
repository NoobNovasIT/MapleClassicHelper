using System.Runtime.InteropServices.WindowsRuntime;
using MapleHelper.Core.Features.Translation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace MapleHelper.Infrastructure.Translation;

/// <summary>
/// OCR có sẵn của Windows (Windows.Media.Ocr), chạy offline. Ưu tiên engine tiếng Anh vì chữ trong game là tiếng Anh.
/// Ảnh được phóng to trước khi đọc vì font pixel của MapleStory rất nhỏ.
/// </summary>
public sealed class WindowsOcrRecognizer : ITextRecognizer
{
    /// <summary>Hệ số phóng to ảnh trước khi OCR; font pixel của game đọc tốt hơn hẳn ở khoảng 3x.</summary>
    private const double TargetScale = 3.0;

    private readonly Lazy<OcrEngine?> _engine = new(CreateEngine);

    public async Task<IReadOnlyList<string>> RecognizeLinesAsync(byte[] png, CancellationToken ct = default)
    {
        var engine = _engine.Value ?? throw new MachineTranslationException(
            "Windows chưa có gói nhận dạng chữ tiếng Anh. Vào Settings → Time & language → Language → thêm English (United States).");

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer()).AsTask(ct);
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        var scale = Math.Min(TargetScale, OcrEngine.MaxImageDimension / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)(decoder.PixelWidth * scale),
            ScaledHeight = (uint)(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct);

        var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
        return result.Lines.Select(l => l.Text).ToList();
    }

    private static OcrEngine? CreateEngine()
    {
        foreach (var tag in new[] { "en-US", "en-GB", "en" })
        {
            var language = new Language(tag);
            if (OcrEngine.IsLanguageSupported(language))
            {
                return OcrEngine.TryCreateFromLanguage(language);
            }
        }
        return OcrEngine.AvailableRecognizerLanguages
            .Where(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            .Select(OcrEngine.TryCreateFromLanguage)
            .FirstOrDefault(e => e is not null);
    }
}
