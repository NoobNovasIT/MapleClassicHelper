using System.Text;

namespace MapleHelper.Core.Features.Translation;

/// <summary>OCR: đọc chữ trong ảnh, chạy trên máy (không gọi mạng).</summary>
public interface ITextRecognizer
{
    /// <summary>Trả về từng dòng chữ đọc được, theo thứ tự từ trên xuống.</summary>
    Task<IReadOnlyList<string>> RecognizeLinesAsync(byte[] png, CancellationToken ct = default);
}

public static class OcrText
{
    /// <summary>
    /// Hội thoại trong game tự xuống dòng giữa câu. Nối các dòng thành đoạn để Google dịch cả câu,
    /// chỉ giữ xuống dòng khi dòng trước kết thúc câu (. ! ? :) hoặc dòng sau là gạch đầu dòng.
    /// </summary>
    public static string JoinLines(IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (sb.Length > 0)
            {
                var previous = sb[^1];
                var startsBullet = line[0] is '-' or '•' or '*' || char.IsDigit(line[0]) && line.Length > 1 && line[1] is '.' or ')';
                sb.Append(previous is '.' or '!' or '?' or ':' || startsBullet ? '\n' : ' ');
            }
            sb.Append(line);
        }
        return sb.ToString();
    }
}
