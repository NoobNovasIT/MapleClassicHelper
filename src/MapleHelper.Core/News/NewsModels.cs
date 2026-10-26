using System.Text;
using System.Text.RegularExpressions;

namespace MapleHelper.Core.News;

public enum NewsCategory
{
    General,
    Update,
    Sale,
    Events,
    Community,
    Maintenance,
    Other,
}

public sealed partial class NewsArticle
{
    public long Id { get; set; }
    public NewsCategory Category { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTimeOffset LiveDate { get; set; }
    public bool IsClassicWorld { get; set; }
    public bool Featured { get; set; }

    public string? TitleVi { get; set; }
    public string? SummaryVi { get; set; }
    public string? BodyHtml { get; set; }
    public string? BodyVi { get; set; }

    public bool IsSaved { get; set; }

    public bool IsTranslated => !string.IsNullOrWhiteSpace(TitleVi);

    /// <summary>Link bài gốc trên nexon.com. Router của trang chỉ dựa vào id, slug để đẹp.</summary>
    public string Url => $"https://www.nexon.com/maplestory/news/{Category.ToString().ToLowerInvariant()}/{Id}/{Slug(Title)}";

    internal static string Slug(string title)
    {
        var slug = NonSlugChars().Replace(title.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "post" : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}

public sealed record NewsQuery(NewsCategory? Category = null, bool ClassicOnly = true, bool SavedOnly = false, int Limit = 100);

public interface INewsSource
{
    Task<IReadOnlyList<NewsArticle>> FetchListAsync(CancellationToken ct = default);
    Task<string> FetchBodyHtmlAsync(long id, CancellationToken ct = default);
}

public interface INewsRepository
{
    /// <summary>Thêm bài mới, cập nhật thông tin gốc của bài cũ (giữ nguyên bản dịch). Trả về id các bài mới.</summary>
    Task<IReadOnlyList<long>> UpsertAsync(IEnumerable<NewsArticle> articles, CancellationToken ct = default);

    Task<IReadOnlyList<NewsArticle>> ListAsync(NewsQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<NewsArticle>> ListUntranslatedAsync(bool classicOnly, int limit, CancellationToken ct = default);
    Task<NewsArticle?> GetAsync(long id, CancellationToken ct = default);
    Task UpdateTranslationAsync(long id, string titleVi, string summaryVi, CancellationToken ct = default);
    Task UpdateBodyAsync(long id, string bodyHtml, string? bodyVi, CancellationToken ct = default);
    Task SetSavedAsync(long id, bool saved, CancellationToken ct = default);
}

/// <summary>Chuyển HTML bài viết thành text dạng markdown đơn giản để dịch và hiển thị.</summary>
public static partial class HtmlText
{
    public static string ToMarkdown(string html)
    {
        var text = html;
        text = ScriptOrStyle().Replace(text, "");
        text = Heading().Replace(text, m => $"\n\n{new string('#', Math.Min(3, int.Parse(m.Groups[1].Value)))} ");
        text = HeadingClose().Replace(text, "\n\n");
        text = ListItem().Replace(text, "\n- ");
        text = Strong().Replace(text, "**");
        text = TableCellBreak().Replace(text, " | ");
        text = BlockBreak().Replace(text, "\n");
        text = Tag().Replace(text, "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = text.Replace(' ', ' ');

        // Gọn khoảng trắng: bỏ khoảng trắng đầu/cuối dòng, gộp nhiều dòng trống
        var sb = new StringBuilder();
        var blank = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = MultiSpace().Replace(raw, " ").Trim();
            if (line is "**" or "**:**" or "|")
            {
                line = "";
            }
            if (line.Length == 0)
            {
                if (++blank <= 1 && sb.Length > 0) sb.Append('\n');
                continue;
            }
            blank = 0;
            sb.Append(line).Append('\n');
        }
        return sb.ToString().Trim();
    }

    [GeneratedRegex(@"<(script|style)[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"<h([1-6])[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Heading();

    [GeneratedRegex(@"</h[1-6]>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingClose();

    [GeneratedRegex(@"<li[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</?(strong|b)(\s[^>]*)?>", RegexOptions.IgnoreCase)]
    private static partial Regex Strong();

    [GeneratedRegex(@"</t[dh]>\s*<t[dh][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex TableCellBreak();

    [GeneratedRegex(@"<br\s*/?>|</p>|<p[^>]*>|</div>|<div[^>]*>|</tr>|</ul>|<ul[^>]*>|</ol>|<ol[^>]*>|</li>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreak();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultiSpace();
}
