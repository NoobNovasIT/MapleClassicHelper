using MapleHelper.Core.Ai;
using MapleHelper.Core.History;
using MapleHelper.Core.News;
using MapleHelper.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace MapleHelper.Infrastructure.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "MapleHelperTests", Guid.NewGuid().ToString("N")));
    private readonly AppDatabase _database;

    public RepositoryTests()
    {
        _database = new AppDatabase(_paths);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_paths.Root, recursive: true);
    }

    private static NewsArticle Article(long id, string title, bool classic = true) => new()
    {
        Id = id,
        Title = title,
        Summary = "summary " + id,
        Category = NewsCategory.General,
        LiveDate = DateTimeOffset.UtcNow.AddDays(-id),
        IsClassicWorld = classic,
    };

    [Fact]
    public async Task Upsert_reports_new_ids_and_keeps_translation_when_unchanged()
    {
        var repo = new NewsRepository(_database);
        Assert.Equal([1L, 2L], await repo.UpsertAsync([Article(1, "A"), Article(2, "B")], Ct));
        await repo.UpdateTranslationAsync(1, "A vi", "tóm tắt", Ct);

        var newIds = await repo.UpsertAsync([Article(1, "A"), Article(2, "B"), Article(3, "C")], Ct);

        Assert.Equal([3L], newIds);
        Assert.Equal("A vi", (await repo.GetAsync(1, Ct))!.TitleVi);
    }

    [Fact]
    public async Task Upsert_clears_translation_when_title_changes()
    {
        var repo = new NewsRepository(_database);
        await repo.UpsertAsync([Article(1, "Old")], Ct);
        await repo.UpdateTranslationAsync(1, "Cũ", "", Ct);

        await repo.UpsertAsync([Article(1, "New title")], Ct);

        var article = await repo.GetAsync(1, Ct);
        Assert.Null(article!.TitleVi);
        Assert.Single(await repo.ListUntranslatedAsync(classicOnly: true, 10, Ct));
    }

    [Fact]
    public async Task List_filters_classic_and_saved()
    {
        var repo = new NewsRepository(_database);
        await repo.UpsertAsync([Article(1, "Classic"), Article(2, "Modern", classic: false)], Ct);
        await repo.SetSavedAsync(2, true, Ct);

        Assert.Single(await repo.ListAsync(new NewsQuery(ClassicOnly: true), Ct));
        Assert.Equal(2, (await repo.ListAsync(new NewsQuery(ClassicOnly: false), Ct)).Count);
        Assert.Equal(2, Assert.Single(await repo.ListAsync(new NewsQuery(ClassicOnly: false, SavedOnly: true), Ct)).Id);
    }

    [Fact]
    public async Task History_round_trips_sources_and_image_and_clears_unsaved()
    {
        var repo = new HistoryRepository(_database);
        var keptId = await repo.AddAsync(new HistoryEntry
        {
            Kind = HistoryKind.ItemLookup,
            Title = "Red Potion",
            Input = "Red Potion",
            Result = "## Vật phẩm",
            Sources = [new AiSource("Wiki", "https://wiki.example")],
            ImagePng = [1, 2, 3],
        }, Ct);
        await repo.AddAsync(new HistoryEntry { Kind = HistoryKind.ChatTranslate, Input = "chào", Result = "hi" }, Ct);
        await repo.SetSavedAsync(keptId, true, Ct);

        await repo.ClearUnsavedAsync(Ct);

        var entry = Assert.Single(await repo.ListAsync(ct: Ct));
        Assert.Equal(HistoryKind.ItemLookup, entry.Kind);
        Assert.True(entry.IsSaved);
        Assert.Equal("https://wiki.example", Assert.Single(entry.Sources).Url);
        Assert.Equal([1, 2, 3], entry.ImagePng);
    }
}
