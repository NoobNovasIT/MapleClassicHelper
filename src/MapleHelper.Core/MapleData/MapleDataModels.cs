namespace MapleHelper.Core.MapleData;

public sealed record MapleItem(int Id, string Name, string? Description, string? Category, string? SubCategory)
{
    public byte[]? IconPng { get; init; }
}

public sealed record MapleQuestSummary(int Id, string Name, int? MinLevel);

/// <summary>Tóm tắt quest dạng text tiếng Anh (NPC, yêu cầu, phần thưởng, lời thoại) để đưa cho AI.</summary>
public sealed record MapleQuestFacts(int Id, string Name, string FactSheet);

/// <summary>
/// Dữ liệu game từ maplestory.io (giải nén từ file WZ). Dùng GMS v83 làm gần đúng cho Classic World.
/// Không có bảng drop (drop nằm phía server), phần đó phải tra web.
/// </summary>
public interface IMapleDataClient
{
    Task<IReadOnlyList<MapleItem>> SearchItemsAsync(string name, int count = 10, CancellationToken ct = default);
    Task<MapleItem?> GetItemAsync(int id, bool includeIcon = true, CancellationToken ct = default);
    Task<IReadOnlyList<MapleQuestSummary>> SearchQuestsAsync(string name, int count = 10, CancellationToken ct = default);
    Task<MapleQuestFacts?> GetQuestFactsAsync(int id, CancellationToken ct = default);
}

public static class MapleNameMatch
{
    /// <summary>Chọn kết quả khớp tên nhất: trùng hẳn, rồi bắt đầu bằng, rồi chứa, cuối cùng là kết quả đầu.</summary>
    public static T? Best<T>(IReadOnlyList<T> candidates, string query, Func<T, string> name) where T : class
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        var q = Normalize(query);
        return candidates.FirstOrDefault(c => Normalize(name(c)) == q)
            ?? candidates.FirstOrDefault(c => Normalize(name(c)).StartsWith(q, StringComparison.Ordinal))
            ?? candidates.FirstOrDefault(c => Normalize(name(c)).Contains(q, StringComparison.Ordinal))
            ?? candidates[0];
    }

    private static string Normalize(string s) =>
        new(s.ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray());
}
