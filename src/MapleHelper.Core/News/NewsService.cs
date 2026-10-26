using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.Settings;

namespace MapleHelper.Core.News;

public interface INewsService
{
    /// <summary>Phát khi danh sách hoặc bản dịch thay đổi (có thể từ thread nền).</summary>
    event EventHandler? Updated;

    /// <summary>Phát khi phát hiện bài mới sau lần làm mới đầu tiên.</summary>
    event EventHandler<IReadOnlyList<NewsArticle>>? NewArticlesFound;

    Task<int> RefreshAsync(CancellationToken ct = default);
    Task<int> TranslatePendingAsync(CancellationToken ct = default);
    Task<NewsArticle?> GetDetailAsync(long id, CancellationToken ct = default);
}

/// <summary>Tin tức dịch bằng Google Dịch (nhanh, không tốn quota AI). AI chỉ dùng cho tìm item và quest.</summary>
public sealed class NewsService(
    INewsSource source,
    INewsRepository repository,
    IMachineTranslator translator,
    ISettingsService settings) : INewsService
{
    /// <summary>Nghỉ giữa các request để không bị Google chặn vì gọi dồn.</summary>
    private static readonly TimeSpan RequestGap = TimeSpan.FromMilliseconds(150);

    // Giữ nguyên tên riêng (MapleStory Classic World, Cash Shop...) thay vì để Google dịch nghĩa
    private readonly IMachineTranslator _translator = new GlossaryTranslator(translator, ProtectedTerms.NewsGlossary);
    private readonly SemaphoreSlim _translateLock = new(1, 1);
    private bool _hasRefreshedOnce;

    public event EventHandler? Updated;
    public event EventHandler<IReadOnlyList<NewsArticle>>? NewArticlesFound;

    public async Task<int> RefreshAsync(CancellationToken ct = default)
    {
        var articles = await source.FetchListAsync(ct);
        var newIds = await repository.UpsertAsync(articles, ct);

        // Lần đầu chạy, cả trăm bài đều "mới" nên không báo
        if (_hasRefreshedOnce && newIds.Count > 0)
        {
            var classicOnly = settings.Current.News.ClassicOnly;
            var fresh = articles.Where(a => newIds.Contains(a.Id) && (!classicOnly || a.IsClassicWorld)).ToList();
            if (fresh.Count > 0)
            {
                NewArticlesFound?.Invoke(this, fresh);
            }
        }
        _hasRefreshedOnce = true;

        Updated?.Invoke(this, EventArgs.Empty);
        return newIds.Count;
    }

    public async Task<int> TranslatePendingAsync(CancellationToken ct = default)
    {
        if (!await _translateLock.WaitAsync(0, ct))
        {
            return 0; // đang có lượt dịch khác chạy
        }
        try
        {
            var translated = 0;
            var pending = await repository.ListUntranslatedAsync(settings.Current.News.ClassicOnly, limit: 60, ct);
            foreach (var article in pending)
            {
                var (title, summary) = await TranslateHeadlineAsync(article, ct);
                await repository.UpdateTranslationAsync(article.Id, title, summary, ct);
                translated++;

                // Cập nhật danh sách theo từng đợt nhỏ để người dùng thấy bản dịch hiện dần
                if (translated % 5 == 0)
                {
                    Updated?.Invoke(this, EventArgs.Empty);
                }
                await Task.Delay(RequestGap, ct);
            }
            if (translated > 0)
            {
                Updated?.Invoke(this, EventArgs.Empty);
            }
            return translated;
        }
        finally
        {
            _translateLock.Release();
        }
    }

    public async Task<NewsArticle?> GetDetailAsync(long id, CancellationToken ct = default)
    {
        var article = await repository.GetAsync(id, ct);
        if (article is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(article.BodyHtml))
        {
            article.BodyHtml = await source.FetchBodyHtmlAsync(id, ct);
            await repository.UpdateBodyAsync(id, article.BodyHtml, null, ct);
        }

        if (string.IsNullOrEmpty(article.BodyVi))
        {
            // Thêm giờ VN vào bản gốc trước khi dịch, Google giữ nguyên phần trong ngoặc
            var markdown = VietnamTime.Annotate(HtmlText.ToMarkdown(article.BodyHtml), article.LiveDate);
            article.BodyVi = await MarkdownMachineTranslation.TranslateAsync(_translator, markdown, ct);
            await repository.UpdateBodyAsync(id, article.BodyHtml, article.BodyVi, ct);
            Updated?.Invoke(this, EventArgs.Empty);
        }

        return article;
    }

    /// <summary>Tiêu đề và tóm tắt gửi chung một request, mỗi phần một dòng.</summary>
    private async Task<(string Title, string Summary)> TranslateHeadlineAsync(NewsArticle article, CancellationToken ct)
    {
        var title = VietnamTime.Annotate(article.Title, article.LiveDate);
        var summary = VietnamTime.Annotate(article.Summary.Replace('\n', ' ').Trim(), article.LiveDate);
        if (summary.Length == 0)
        {
            return ((await _translator.TranslateAsync(title, "en", "vi", ct)).Trim(), "");
        }

        var translated = await _translator.TranslateAsync(title + "\n" + summary, "en", "vi", ct);
        if (SplitHeadline(translated) is { } split)
        {
            return split;
        }

        // Google gộp mất dòng: dịch riêng từng phần
        await Task.Delay(RequestGap, ct);
        var titleVi = await _translator.TranslateAsync(title, "en", "vi", ct);
        await Task.Delay(RequestGap, ct);
        var summaryVi = await _translator.TranslateAsync(summary, "en", "vi", ct);
        return (titleVi.Trim(), summaryVi.Trim());
    }

    internal static (string Title, string Summary)? SplitHeadline(string translated)
    {
        var lines = translated.Replace("\r", "").Split('\n', 2);
        return lines.Length == 2 && lines[0].Trim().Length > 0
            ? (lines[0].Trim(), lines[1].Replace('\n', ' ').Trim())
            : null;
    }
}
