using MapleHelper.Core.Ai;

namespace MapleHelper.Core.History;

public enum HistoryKind
{
    ScreenTranslate,
    ItemLookup,
    QuestGuide,
    ChatTranslate,
}

public sealed class HistoryEntry
{
    public long Id { get; set; }
    public HistoryKind Kind { get; set; }

    /// <summary>Tên ngắn hiển thị trong danh sách (tên item, tên quest...). Null thì dùng Input.</summary>
    public string? Title { get; set; }

    public string Input { get; set; } = "";
    public string Result { get; set; } = "";
    public IReadOnlyList<AiSource> Sources { get; set; } = [];

    /// <summary>Ảnh vùng chụp (PNG), để xem lại. Chat không có ảnh.</summary>
    public byte[]? ImagePng { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public bool IsSaved { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Input : Title;
}

public interface IHistoryRepository
{
    event EventHandler? Changed;

    Task<long> AddAsync(HistoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<HistoryEntry>> ListAsync(HistoryKind? kind = null, bool savedOnly = false, int limit = 200, CancellationToken ct = default);
    Task SetSavedAsync(long id, bool saved, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>Xoá toàn bộ lịch sử, giữ lại mục đã lưu.</summary>
    Task ClearUnsavedAsync(CancellationToken ct = default);
}
