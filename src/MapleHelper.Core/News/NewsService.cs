using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;
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

public sealed class NewsService(
    INewsSource source,
    INewsRepository repository,
    IAiService ai,
    ISettingsService settings) : INewsService
{
    private const int TranslateBatchSize = 15;
    private const int BodyChunkChars = 7000;

    internal const string SystemPrompt =
        "Bạn dịch tin tức chính thức của MapleStory (Nexon) sang tiếng Việt cho cộng đồng người chơi Việt Nam. Văn phong tự nhiên, rõ ràng.";

    internal const string ListPrompt = """
        Dịch "title" và "summary" của từng tin dưới đây sang tiếng Việt.
        - Giữ nguyên tên riêng: tên game, tên sự kiện, vật phẩm, NPC, bản đồ, số phiên bản (v.271...).
        - "[Completed]" dịch là "[Đã xong]", "[Updated]" là "[Cập nhật]".
        - Ngắn gọn như tiêu đề báo.
        Chỉ trả về JSON array cùng thứ tự, mỗi phần tử: {"id": <id>, "title": "...", "summary": "..."}

        """;

    internal const string BodyPrompt = """
        Dịch đoạn bài viết sau (định dạng markdown) sang tiếng Việt.
        - Giữ nguyên định dạng markdown (#, -, **, |) và xuống dòng.
        - Giữ nguyên tên riêng: vật phẩm, NPC, bản đồ, quái, kỹ năng, sự kiện, nút bấm trong game.
        - Mốc thời gian (PDT, PST, PT, UTC, EDT...): giữ nguyên và thêm giờ Việt Nam (GMT+7) trong ngoặc,
          ví dụ "4:00 AM PDT" thành "4:00 AM PDT (18:00 giờ VN)". Nếu đổi giờ làm sang ngày khác thì ghi rõ ngày.
        Chỉ trả về bản dịch, không thêm lời dẫn.

        """;

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
            foreach (var batch in pending.Chunk(TranslateBatchSize))
            {
                var results = await TranslateListAsync(batch, ct);
                foreach (var (id, title, summary) in results)
                {
                    await repository.UpdateTranslationAsync(id, title, summary, ct);
                    translated++;
                }
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
            var markdown = HtmlText.ToMarkdown(article.BodyHtml);
            var translated = new StringBuilder();
            foreach (var chunk in SplitChunks(markdown, BodyChunkChars))
            {
                var response = await ai.GenerateAsync(new AiRequest
                {
                    SystemPrompt = SystemPrompt,
                    Prompt = BodyPrompt + chunk,
                    MaxOutputTokens = 16000,
                }, ct);
                translated.Append(response.Text.Trim()).Append("\n\n");
            }
            article.BodyVi = translated.ToString().Trim();
            await repository.UpdateBodyAsync(id, article.BodyHtml, article.BodyVi, ct);
            Updated?.Invoke(this, EventArgs.Empty);
        }

        return article;
    }

    private async Task<IReadOnlyList<(long Id, string Title, string Summary)>> TranslateListAsync(
        IReadOnlyList<NewsArticle> batch, CancellationToken ct)
    {
        var input = new JsonArray(batch.Select(a => (JsonNode)new JsonObject
        {
            ["id"] = a.Id,
            ["title"] = a.Title,
            ["summary"] = a.Summary,
        }).ToArray());

        var response = await ai.GenerateAsync(new AiRequest
        {
            SystemPrompt = SystemPrompt,
            Prompt = ListPrompt + input.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            MaxOutputTokens = 8000,
        }, ct);

        return ParseListTranslation(response.Text, batch.Select(a => a.Id).ToHashSet());
    }

    internal static IReadOnlyList<(long Id, string Title, string Summary)> ParseListTranslation(string text, IReadOnlySet<long> expectedIds)
    {
        if (AiJson.TryParse(text) is not JsonArray array)
        {
            return [];
        }

        var results = new List<(long, string, string)>();
        foreach (var item in array)
        {
            if (item?["id"] is not JsonValue idValue || !long.TryParse(idValue.ToString(), out var id) || !expectedIds.Contains(id))
            {
                continue;
            }
            var title = item.GetString("title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }
            results.Add((id, title.Trim(), item.GetString("summary")?.Trim() ?? ""));
        }
        return results;
    }

    /// <summary>Cắt theo đoạn (dòng trống) để không cắt giữa câu.</summary>
    internal static IEnumerable<string> SplitChunks(string text, int maxChars)
    {
        var current = new StringBuilder();
        foreach (var paragraph in text.Split("\n\n"))
        {
            if (current.Length > 0 && current.Length + paragraph.Length > maxChars)
            {
                yield return current.ToString();
                current.Clear();
            }
            current.Append(paragraph).Append("\n\n");
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
