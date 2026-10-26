using System.Globalization;
using Dapper;
using MapleHelper.Core.News;

namespace MapleHelper.Infrastructure.Persistence;

public sealed class NewsRepository(AppDatabase database) : INewsRepository
{
    private const string SelectColumns = """
        SELECT id, category, title, summary, image_url AS ImageUrl, live_date AS LiveDate, is_classic AS IsClassic,
               featured, title_vi AS TitleVi, summary_vi AS SummaryVi, body_html AS BodyHtml, body_vi AS BodyVi,
               is_saved AS IsSaved
        FROM news_articles
        """;

    public async Task<IReadOnlyList<long>> UpsertAsync(IEnumerable<NewsArticle> articles, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var existing = (await connection.QueryAsync<long>("SELECT id FROM news_articles", transaction: transaction)).ToHashSet();
        var newIds = new List<long>();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        foreach (var a in articles)
        {
            var isNew = !existing.Contains(a.Id);
            // Bài cũ bị sửa tiêu đề/tóm tắt trên web thì xoá bản dịch để dịch lại
            await connection.ExecuteAsync("""
                INSERT INTO news_articles (id, category, title, summary, image_url, live_date, is_classic, featured, fetched_at)
                VALUES (@Id, @Category, @Title, @Summary, @ImageUrl, @LiveDate, @IsClassic, @Featured, @Now)
                ON CONFLICT(id) DO UPDATE SET
                    title_vi = CASE WHEN title <> excluded.title OR summary <> excluded.summary THEN NULL ELSE title_vi END,
                    summary_vi = CASE WHEN title <> excluded.title OR summary <> excluded.summary THEN NULL ELSE summary_vi END,
                    category = excluded.category, title = excluded.title, summary = excluded.summary,
                    image_url = excluded.image_url, live_date = excluded.live_date,
                    is_classic = excluded.is_classic, featured = excluded.featured, fetched_at = excluded.fetched_at
                """, new
            {
                a.Id,
                Category = a.Category.ToString(),
                a.Title,
                a.Summary,
                a.ImageUrl,
                LiveDate = a.LiveDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                IsClassic = a.IsClassicWorld ? 1 : 0,
                Featured = a.Featured ? 1 : 0,
                Now = now,
            }, transaction);

            if (isNew)
            {
                newIds.Add(a.Id);
            }
        }

        await transaction.CommitAsync(ct);
        return newIds;
    }

    public async Task<IReadOnlyList<NewsArticle>> ListAsync(NewsQuery query, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        var rows = await connection.QueryAsync<Row>(SelectColumns + """

            WHERE (@Category IS NULL OR category = @Category)
              AND (@ClassicOnly = 0 OR is_classic = 1)
              AND (@SavedOnly = 0 OR is_saved = 1)
            ORDER BY live_date DESC
            LIMIT @Limit
            """, new
        {
            Category = query.Category?.ToString(),
            ClassicOnly = query.ClassicOnly ? 1 : 0,
            SavedOnly = query.SavedOnly ? 1 : 0,
            query.Limit,
        });
        return rows.Select(ToArticle).ToList();
    }

    public async Task<IReadOnlyList<NewsArticle>> ListUntranslatedAsync(bool classicOnly, int limit, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        var rows = await connection.QueryAsync<Row>(SelectColumns + """

            WHERE title_vi IS NULL AND (@ClassicOnly = 0 OR is_classic = 1)
            ORDER BY live_date DESC
            LIMIT @Limit
            """, new { ClassicOnly = classicOnly ? 1 : 0, Limit = limit });
        return rows.Select(ToArticle).ToList();
    }

    public async Task<NewsArticle?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(SelectColumns + " WHERE id = @Id", new { Id = id });
        return row is null ? null : ToArticle(row);
    }

    public async Task UpdateTranslationAsync(long id, string titleVi, string summaryVi, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        await connection.ExecuteAsync("UPDATE news_articles SET title_vi = @TitleVi, summary_vi = @SummaryVi WHERE id = @Id",
            new { Id = id, TitleVi = titleVi, SummaryVi = summaryVi });
    }

    public async Task UpdateBodyAsync(long id, string bodyHtml, string? bodyVi, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        await connection.ExecuteAsync("UPDATE news_articles SET body_html = @BodyHtml, body_vi = @BodyVi WHERE id = @Id",
            new { Id = id, BodyHtml = bodyHtml, BodyVi = bodyVi });
    }

    public async Task SetSavedAsync(long id, bool saved, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        await connection.ExecuteAsync("UPDATE news_articles SET is_saved = @Saved WHERE id = @Id", new { Id = id, Saved = saved ? 1 : 0 });
    }

    private static NewsArticle ToArticle(Row row) => new()
    {
        Id = row.Id,
        Category = Enum.TryParse<NewsCategory>(row.Category, out var category) ? category : NewsCategory.Other,
        Title = row.Title,
        Summary = row.Summary,
        ImageUrl = row.ImageUrl,
        LiveDate = DateTimeOffset.Parse(row.LiveDate, CultureInfo.InvariantCulture),
        IsClassicWorld = row.IsClassic != 0,
        Featured = row.Featured != 0,
        TitleVi = row.TitleVi,
        SummaryVi = row.SummaryVi,
        BodyHtml = row.BodyHtml,
        BodyVi = row.BodyVi,
        IsSaved = row.IsSaved != 0,
    };

    private sealed class Row
    {
        public long Id { get; set; }
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string LiveDate { get; set; } = "";
        public long IsClassic { get; set; }
        public long Featured { get; set; }
        public string? TitleVi { get; set; }
        public string? SummaryVi { get; set; }
        public string? BodyHtml { get; set; }
        public string? BodyVi { get; set; }
        public long IsSaved { get; set; }
    }
}
