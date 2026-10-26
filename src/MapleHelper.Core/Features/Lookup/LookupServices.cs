using MapleHelper.Core.Ai;
using MapleHelper.Core.MapleData;

namespace MapleHelper.Core.Features.Lookup;

public sealed record ItemLookupResult(string ItemName, MapleItem? Item, string Guide, IReadOnlyList<AiSource> Sources);

public sealed record QuestGuideResult(string QuestName, MapleQuestSummary? Quest, string Guide, IReadOnlyList<AiSource> Sources)
{
    public string YouTubeSearchUrl =>
        "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString($"MapleStory Classic {QuestName} quest");
}

public interface IItemLookupService
{
    /// <summary>Đọc tên vật phẩm trong ảnh (tooltip/ô đồ). Trả về chuỗi rỗng nếu không thấy.</summary>
    Task<string> ReadItemNameAsync(byte[] png, CancellationToken ct = default);
    Task<ItemLookupResult> LookupAsync(string itemName, CancellationToken ct = default);
}

public interface IQuestGuideService
{
    Task<string> ReadQuestNameAsync(byte[] png, CancellationToken ct = default);
    Task<QuestGuideResult> GuideAsync(string questName, CancellationToken ct = default);
}

internal static class LookupPrompts
{
    public const string Context =
        "Người chơi đang chơi MapleStory Classic World của Nexon (bản cổ điển, nội dung gần với GMS khoảng v62 đến v83).";

    public const string ReadItemName = """
        Ảnh là tooltip hoặc ô vật phẩm trong game MapleStory.
        Đọc tên vật phẩm tiếng Anh chính xác như trong ảnh (thường là dòng chữ đậm trên cùng).
        Chỉ trả về JSON: {"name": "<tên vật phẩm>"}. Không thấy tên thì {"name": ""}.
        """;

    public const string ReadQuestName = """
        Ảnh là cửa sổ nhiệm vụ (quest) hoặc hội thoại NPC trong game MapleStory.
        Đọc tên nhiệm vụ tiếng Anh chính xác như trong ảnh.
        Chỉ trả về JSON: {"name": "<tên nhiệm vụ>"}. Không thấy tên thì {"name": ""}.
        """;

    public const string ItemGuide = """
        {0}
        Người chơi muốn biết nguồn gốc vật phẩm: "{1}".

        Dữ liệu tham khảo từ file game GMS v83 (có thể khác Classic World một chút):
        {2}

        Hãy tra web (ưu tiên wiki và hướng dẫn cho MapleStory Classic World, rồi đến các bản cổ điển như MapleLegends, MapleRoyals, BasilMarket, Hidden Street)
        và trả lời bằng tiếng Việt theo đúng khung markdown sau:

        ## Vật phẩm
        Một hai câu: loại vật phẩm, công dụng.
        ## Rơi từ quái
        - **Tên quái** (Lv) — bản đồ hay gặp
        ## Nhận từ nhiệm vụ
        - **Tên quest** — NPC giao
        ## Mua hoặc chế tạo
        ## Mẹo
        - Nơi farm hiệu quả, lưu ý.

        Quy tắc: giữ nguyên tên quái, bản đồ, NPC, quest bằng tiếng Anh. Mục nào không có thông tin thì ghi "Chưa tìm thấy thông tin".
        Không bịa số liệu. Ngắn gọn, tối đa khoảng 250 từ.
        """;

    public const string QuestGuide = """
        {0}
        Người chơi cần hướng dẫn làm nhiệm vụ: "{1}".

        Dữ liệu tham khảo từ file game GMS v83 (số lượng hoặc phần thưởng có thể khác Classic World):
        {2}

        Hãy tra web (wiki, hướng dẫn, video YouTube về MapleStory Classic World hoặc các bản cổ điển) và trả lời bằng tiếng Việt theo khung markdown:

        ## Tổng quan
        - NPC giao: **tên NPC** ở **bản đồ**
        - Cấp độ yêu cầu, nghề (nếu có)
        ## Các bước
        1. ...
        ## Cần chuẩn bị
        - Vật phẩm cần thu thập / quái cần săn, số lượng, nơi săn
        ## Phần thưởng
        ## Mẹo
        ## Video
        - Link YouTube hướng dẫn nếu tìm được

        Quy tắc: giữ nguyên tên NPC, quái, bản đồ, vật phẩm bằng tiếng Anh. Không bịa, mục không rõ thì ghi "Chưa tìm thấy thông tin".
        Ngắn gọn, tối đa khoảng 300 từ.
        """;
}

public sealed class ItemLookupService(IAiService ai, IMapleDataClient mapleData) : IItemLookupService
{
    public async Task<string> ReadItemNameAsync(byte[] png, CancellationToken ct = default)
    {
        var response = await ai.GenerateAsync(new AiRequest
        {
            Prompt = LookupPrompts.ReadItemName,
            ImagePng = png,
            MaxOutputTokens = 1024,
        }, ct);
        return ReadName(response.Text);
    }

    public async Task<ItemLookupResult> LookupAsync(string itemName, CancellationToken ct = default)
    {
        var item = await FindItemAsync(itemName, ct);
        var facts = item is null
            ? "(không tìm thấy trong dữ liệu game)"
            : $"Id {item.Id}, tên \"{item.Name}\", loại {item.Category}/{item.SubCategory}. Mô tả: {item.Description}";

        var response = await ai.GenerateAsync(new AiRequest
        {
            Prompt = string.Format(LookupPrompts.ItemGuide, LookupPrompts.Context, item?.Name ?? itemName, facts),
            UseWebSearch = true,
            MaxOutputTokens = 8000,
        }, ct);

        return new ItemLookupResult(item?.Name ?? itemName, item, response.Text.Trim(), response.Sources);
    }

    private async Task<MapleItem?> FindItemAsync(string name, CancellationToken ct)
    {
        try
        {
            var candidates = await mapleData.SearchItemsAsync(name, 10, ct);
            var best = MapleNameMatch.Best(candidates, name, c => c.Name);
            return best is null ? null : await mapleData.GetItemAsync(best.Id, includeIcon: true, ct) ?? best;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null; // maplestory.io lỗi thì vẫn tra web được
        }
    }

    internal static string ReadName(string text) => AiJson.TryParse(text).GetString("name")?.Trim() ?? "";
}

public sealed class QuestGuideService(IAiService ai, IMapleDataClient mapleData) : IQuestGuideService
{
    public async Task<string> ReadQuestNameAsync(byte[] png, CancellationToken ct = default)
    {
        var response = await ai.GenerateAsync(new AiRequest
        {
            Prompt = LookupPrompts.ReadQuestName,
            ImagePng = png,
            MaxOutputTokens = 1024,
        }, ct);
        return ItemLookupService.ReadName(response.Text);
    }

    public async Task<QuestGuideResult> GuideAsync(string questName, CancellationToken ct = default)
    {
        MapleQuestSummary? quest = null;
        MapleQuestFacts? facts = null;
        try
        {
            var candidates = await mapleData.SearchQuestsAsync(questName, 10, ct);
            quest = MapleNameMatch.Best(candidates, questName, c => c.Name);
            if (quest is not null)
            {
                facts = await mapleData.GetQuestFactsAsync(quest.Id, ct);
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Bỏ qua, tra web vẫn chạy
        }

        var name = quest?.Name ?? questName;
        var response = await ai.GenerateAsync(new AiRequest
        {
            Prompt = string.Format(LookupPrompts.QuestGuide, LookupPrompts.Context, name,
                facts?.FactSheet ?? "(không tìm thấy trong dữ liệu game)"),
            UseWebSearch = true,
            MaxOutputTokens = 8000,
        }, ct);

        return new QuestGuideResult(name, quest, response.Text.Trim(), response.Sources);
    }
}
