using System.Globalization;
using System.Text.Json;
using Dapper;
using MapleHelper.Core.Ai;
using MapleHelper.Core.History;

namespace MapleHelper.Infrastructure.Persistence;

public sealed class HistoryRepository(AppDatabase database) : IHistoryRepository
{
    public event EventHandler? Changed;

    public async Task<long> AddAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO history_entries (kind, title, input, result, sources_json, image_png, created_at, is_saved)
            VALUES (@Kind, @Title, @Input, @Result, @SourcesJson, @ImagePng, @CreatedAt, @IsSaved);
            SELECT last_insert_rowid();
            """, new
        {
            Kind = entry.Kind.ToString(),
            entry.Title,
            entry.Input,
            entry.Result,
            SourcesJson = entry.Sources.Count == 0 ? null : JsonSerializer.Serialize(entry.Sources),
            entry.ImagePng,
            CreatedAt = entry.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            IsSaved = entry.IsSaved ? 1 : 0,
        });
        entry.Id = id;
        Changed?.Invoke(this, EventArgs.Empty);
        return id;
    }

    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(HistoryKind? kind = null, bool savedOnly = false, int limit = 200, CancellationToken ct = default)
    {
        await using var connection = await database.OpenAsync(ct);
        var rows = await connection.QueryAsync<Row>("""
            SELECT id, kind, title, input, result, sources_json AS SourcesJson, image_png AS ImagePng,
                   created_at AS CreatedAt, is_saved AS IsSaved
            FROM history_entries
            WHERE (@Kind IS NULL OR kind = @Kind) AND (@SavedOnly = 0 OR is_saved = 1)
            ORDER BY created_at DESC
            LIMIT @Limit
            """, new { Kind = kind?.ToString(), SavedOnly = savedOnly ? 1 : 0, Limit = limit });
        return rows.Select(ToEntry).ToList();
    }

    public Task SetSavedAsync(long id, bool saved, CancellationToken ct = default) =>
        ExecuteAsync("UPDATE history_entries SET is_saved = @Saved WHERE id = @Id", new { Id = id, Saved = saved ? 1 : 0 }, ct);

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        ExecuteAsync("DELETE FROM history_entries WHERE id = @Id", new { Id = id }, ct);

    public Task ClearUnsavedAsync(CancellationToken ct = default) =>
        ExecuteAsync("DELETE FROM history_entries WHERE is_saved = 0", null, ct);

    private async Task ExecuteAsync(string sql, object? param, CancellationToken ct)
    {
        await using var connection = await database.OpenAsync(ct);
        await connection.ExecuteAsync(sql, param);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static HistoryEntry ToEntry(Row row) => new()
    {
        Id = row.Id,
        Kind = Enum.TryParse<HistoryKind>(row.Kind, out var kind) ? kind : HistoryKind.ScreenTranslate,
        Title = row.Title,
        Input = row.Input,
        Result = row.Result,
        Sources = string.IsNullOrEmpty(row.SourcesJson) ? [] : JsonSerializer.Deserialize<List<AiSource>>(row.SourcesJson) ?? [],
        ImagePng = row.ImagePng,
        CreatedAt = DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture),
        IsSaved = row.IsSaved != 0,
    };

    private sealed class Row
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "";
        public string? Title { get; set; }
        public string Input { get; set; } = "";
        public string Result { get; set; } = "";
        public string? SourcesJson { get; set; }
        public byte[]? ImagePng { get; set; }
        public string CreatedAt { get; set; } = "";
        public long IsSaved { get; set; }
    }
}
