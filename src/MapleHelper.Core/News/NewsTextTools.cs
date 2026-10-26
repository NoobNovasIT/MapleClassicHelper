using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MapleHelper.Core.Features.Translation;

namespace MapleHelper.Core.News;

/// <summary>
/// Dịch markdown bằng dịch máy mà không làm hỏng định dạng: tách ký hiệu đầu dòng (#, -, 1.) và ** ra,
/// chỉ gửi phần chữ, rồi ghép lại.
/// </summary>
public static partial class MarkdownMachineTranslation
{
    private const int MaxChunkChars = 4500;

    public static async Task<string> TranslateAsync(IMachineTranslator translator, string markdown, CancellationToken ct)
    {
        var lines = markdown.Replace("\r", "").Split('\n').Select(ParseLine).ToList();
        var textLines = lines.Where(l => l.Text.Length > 0).ToList();

        // Gửi nhiều dòng một lần (ngăn bằng \n) để ít request; nếu số dòng trả về lệch thì dịch lại từng dòng
        foreach (var chunk in Chunk(textLines))
        {
            var joined = string.Join("\n", chunk.Select(l => l.Text));
            var translated = (await translator.TranslateAsync(joined, "en", "vi", ct)).Replace("\r", "").Split('\n');
            if (translated.Length == chunk.Count)
            {
                for (var i = 0; i < chunk.Count; i++)
                {
                    chunk[i].Translated = translated[i].Trim();
                }
            }
            else
            {
                foreach (var line in chunk)
                {
                    line.Translated = (await translator.TranslateAsync(line.Text, "en", "vi", ct)).Trim();
                }
            }
        }

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append(line.Render()).Append('\n');
        }
        return sb.ToString().Trim();
    }

    private static IEnumerable<List<Line>> Chunk(List<Line> lines)
    {
        var current = new List<Line>();
        var size = 0;
        foreach (var line in lines)
        {
            if (current.Count > 0 && size + line.Text.Length + 1 > MaxChunkChars)
            {
                yield return current;
                current = [];
                size = 0;
            }
            current.Add(line);
            size += line.Text.Length + 1;
        }
        if (current.Count > 0)
        {
            yield return current;
        }
    }

    internal static Line ParseLine(string raw)
    {
        var match = LinePrefix().Match(raw);
        var prefix = match.Groups["prefix"].Value;
        var body = raw[match.Length..].Trim();
        var bold = body.Length > 4 && body.StartsWith("**", StringComparison.Ordinal) && body.EndsWith("**", StringComparison.Ordinal)
                   && body.IndexOf("**", 2, StringComparison.Ordinal) == body.Length - 2;
        // ** ở giữa câu bỏ đi: Google hay chèn khoảng trắng làm vỡ định dạng
        var text = (bold ? body[2..^2] : body).Replace("**", "").Trim();
        return new Line(prefix, text, bold);
    }

    [GeneratedRegex(@"^\s*(?<prefix>#{1,4}\s+|[-*•]\s+|\d+[.)]\s+)?")]
    private static partial Regex LinePrefix();

    internal sealed class Line(string prefix, string text, bool bold)
    {
        public string Prefix { get; } = prefix;
        public string Text { get; } = text;
        public bool Bold { get; } = bold;
        public string? Translated { get; set; }

        public string Render()
        {
            if (Text.Length == 0)
            {
                return Prefix.TrimEnd();
            }
            var body = Translated ?? Text;
            return Prefix + (Bold ? $"**{body}**" : body);
        }
    }
}

/// <summary>
/// Giữ nguyên tên riêng khi dịch máy: thay bằng mã tạm (Google không dịch chuỗi kiểu "ZQX0Q") rồi trả lại sau khi dịch.
/// </summary>
public sealed class ProtectedTerms
{
    /// <summary>Tên riêng hay gặp trong tin Nexon. Cụm dài đặt trước để không bị cụm ngắn ăn mất.</summary>
    public static readonly IReadOnlyList<string> NewsGlossary =
    [
        "MapleStory Classic World", "Classic World", "MapleStory Worlds", "MapleStory",
        "Grand Launch", "Founder's Access", "Cash Shop", "Maple Points", "NX Credit", "NX Prepaid",
        "Megaphone", "Mesos", "Meso", "Maplers", "Mapler", "Henesys", "Kerning City", "Ellinia", "Perion",
        "Lith Harbor", "Sleepywood", "Victoria Island", "Maple Island", "Ossyria", "El Nath", "Ludibrium", "Orbis",
    ];

    private readonly List<string> _found = [];

    public string Protect(string text, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var token = Token(_found.Count);
                _found.Add(text.Substring(index, term.Length));
                text = text[..index] + token + text[(index + term.Length)..];
                index = text.IndexOf(term, index + token.Length, StringComparison.OrdinalIgnoreCase);
            }
        }
        return text;
    }

    public string Restore(string text)
    {
        for (var i = _found.Count - 1; i >= 0; i--)
        {
            text = text.Replace(Token(i), _found[i], StringComparison.OrdinalIgnoreCase);
        }
        return text;
    }

    private static string Token(int index) => $"ZQX{index}Q";
}

/// <summary>Bọc một <see cref="IMachineTranslator"/> để mọi lần dịch đều giữ nguyên các tên riêng cho trước.</summary>
public sealed class GlossaryTranslator(IMachineTranslator inner, IReadOnlyList<string> terms) : IMachineTranslator
{
    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false)
    {
        var protectedTerms = new ProtectedTerms();
        var translated = await inner.TranslateAsync(protectedTerms.Protect(text, terms), from, to, ct, background);
        return protectedTerms.Restore(translated);
    }
}

/// <summary>
/// Thêm giờ Việt Nam sau các mốc giờ trong tin Nexon, ví dụ "4:00 AM PDT" → "4:00 AM PDT (18:00 giờ VN)".
/// Khoảng "4:00 AM - 10:00 AM PDT" đổi cả hai đầu. Ngày lấy theo ngày đăng bài để biết có lệch sang hôm sau không.
/// </summary>
public static partial class VietnamTime
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static string Annotate(string text, DateTimeOffset referenceDate)
    {
        return TimePattern().Replace(text, match =>
        {
            var zone = match.Groups["zone"].Value.ToUpperInvariant();
            var offset = ZoneOffset(zone, referenceDate);
            // "4:00 AM PDT (11:00 UTC)": phần UTC trong ngoặc chỉ là chú thích, giờ VN đã thêm ở mốc PDT
            var insideParentheses = match.Index > 0 && text[match.Index - 1] == '(';
            var endMeridiem = match.Groups["m2"].Value;
            // Không có AM/PM thì phải là giờ 24h dạng "11:00", tránh bắt nhầm số đơn lẻ
            var looksLikeTime = endMeridiem.Length > 0 || match.Groups["t2"].Value.Contains(':');
            if (offset is null || !looksLikeTime || (insideParentheses && zone is "UTC" or "GMT"))
            {
                return match.Value;
            }
            var end = ToVietnam(match.Groups["t2"].Value, endMeridiem, offset.Value);
            if (!match.Groups["t1"].Success)
            {
                return $"{match.Value} ({end} giờ VN)";
            }
            // "4:00 - 10:00 AM PDT": đầu thiếu AM/PM thì lấy theo đầu sau
            var startMeridiem = match.Groups["m1"].Success ? match.Groups["m1"].Value : endMeridiem;
            var start = ToVietnam(match.Groups["t1"].Value, startMeridiem, offset.Value);
            return $"{match.Value} ({start} – {end} giờ VN)";
        });
    }

    private static string ToVietnam(string time, string meridiem, TimeSpan zoneOffset)
    {
        var parts = time.Split(':');
        var hour = int.Parse(parts[0], CultureInfo.InvariantCulture);
        if (meridiem.Length > 0)
        {
            hour %= 12;
            if (meridiem.Equals("PM", StringComparison.OrdinalIgnoreCase))
            {
                hour += 12;
            }
        }
        var minute = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;

        var totalMinutes = hour * 60 + minute + (int)(VietnamOffset - zoneOffset).TotalMinutes;
        var dayShift = (int)Math.Floor(totalMinutes / 1440.0);
        totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
        var result = $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
        return dayShift switch
        {
            > 0 => result + " hôm sau",
            < 0 => result + " hôm trước",
            _ => result,
        };
    }

    private static TimeSpan? ZoneOffset(string zone, DateTimeOffset referenceDate) => zone.ToUpperInvariant() switch
    {
        "PDT" => TimeSpan.FromHours(-7),
        "PST" => TimeSpan.FromHours(-8),
        "PT" => PacificOffset(referenceDate),
        "EDT" => TimeSpan.FromHours(-4),
        "EST" => TimeSpan.FromHours(-5),
        "UTC" or "GMT" => TimeSpan.Zero,
        _ => null,
    };

    /// <summary>"PT" không nói rõ giờ mùa hè hay không: lấy theo ngày đăng bài.</summary>
    private static TimeSpan PacificOffset(DateTimeOffset date)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time").GetUtcOffset(date);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeSpan.FromHours(-7);
        }
    }

    [GeneratedRegex(@"(?:(?<t1>\d{1,2}(?::\d{2})?)\s*(?<m1>AM|PM)?\s*[-–~]\s*)?(?<t2>\d{1,2}(?::\d{2})?)\s*(?<m2>AM|PM)?\s*(?<zone>PDT|PST|PT|EDT|EST|UTC|GMT)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();
}
