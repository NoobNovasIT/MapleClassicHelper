using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapleHelper.Infrastructure.Translation;

namespace MapleHelper.Infrastructure.Tests;

/// <summary>Chạy OCR thật của Windows trên ảnh chữ nhỏ (giống hội thoại trong game).</summary>
public class WindowsOcrTests
{
    [Fact]
    public async Task Reads_small_dialog_text()
    {
        var png = RenderText("Bring me 10 Orange Mushroom Caps.\nThen talk to Mai in Amherst.", fontSize: 12);

        var lines = await new WindowsOcrRecognizer().RecognizeLinesAsync(png, TestContext.Current.CancellationToken);
        var text = string.Join(" ", lines);

        Assert.Contains("Orange Mushroom", text);
        Assert.Contains("Amherst", text);
    }

    /// <summary>Vẽ chữ đen trên nền sáng giống khung NPC, trên thread STA riêng vì WPF yêu cầu.</summary>
    private static byte[] RenderText(string text, double fontSize)
    {
        byte[]? result = null;
        var thread = new Thread(() =>
        {
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Tahoma"), fontSize, Brushes.Black, 1.0);
            var width = (int)Math.Ceiling(formatted.Width) + 20;
            var height = (int)Math.Ceiling(formatted.Height) + 20;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xEC, 0xDC)), null, new Rect(0, 0, width, height));
                dc.DrawText(formatted, new Point(10, 10));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            result = stream.ToArray();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result!;
    }
}
