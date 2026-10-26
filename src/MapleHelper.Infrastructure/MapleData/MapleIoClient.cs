using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MapleHelper.Core.MapleData;
using MapleHelper.Core.Settings;

namespace MapleHelper.Infrastructure.MapleData;

/// <summary>Client cho https://maplestory.io/api/{region}/{version}/... (item, quest, npc, mob).</summary>
public sealed class MapleIoClient(HttpClient http, ISettingsService settings) : IMapleDataClient
{
    private string BaseUrl
    {
        get
        {
            var s = settings.Current.MapleData;
            return $"{s.ApiBaseUrl.TrimEnd('/')}/{s.Region}/{s.Version}";
        }
    }

    public async Task<IReadOnlyList<MapleItem>> SearchItemsAsync(string name, int count = 10, CancellationToken ct = default)
    {
        var json = await GetJsonAsync($"/item?searchFor={Uri.EscapeDataString(name)}&count={count}", ct);
        return ParseItemSearch(json);
    }

    public async Task<MapleItem?> GetItemAsync(int id, bool includeIcon = true, CancellationToken ct = default)
    {
        var json = await GetJsonAsync($"/item/{id}", ct);
        return json is null ? null : ParseItemDetail(json, includeIcon);
    }

    public async Task<IReadOnlyList<MapleQuestSummary>> SearchQuestsAsync(string name, int count = 10, CancellationToken ct = default)
    {
        var json = await GetJsonAsync($"/quest?searchFor={Uri.EscapeDataString(name)}&count={count}", ct);
        return json?.AsArray()
            .Where(q => q?["id"] is not null && q["name"] is not null)
            .Select(q => new MapleQuestSummary(q!["id"]!.GetValue<int>(), q["name"]!.GetValue<string>(), q["minLevel"]?.GetValue<int>()))
            .ToList() ?? [];
    }

    public async Task<MapleQuestFacts?> GetQuestFactsAsync(int id, CancellationToken ct = default)
    {
        var json = await GetJsonAsync($"/quest/{id}", ct);
        if (json is null)
        {
            return null;
        }

        // Đổi id NPC/item/quái sang tên để AI viết hướng dẫn dễ hiểu
        var names = new Dictionary<string, string>();
        foreach (var (kind, refId) in CollectReferences(json))
        {
            var key = $"{kind}:{refId}";
            if (!names.ContainsKey(key))
            {
                names[key] = await GetNameAsync(kind, refId, ct) ?? $"#{refId}";
            }
        }
        return new MapleQuestFacts(id, json["name"]?.GetValue<string>() ?? $"Quest {id}", BuildFactSheet(json, names));
    }

    private async Task<string?> GetNameAsync(string kind, int id, CancellationToken ct)
    {
        try
        {
            var json = await GetJsonAsync($"/{kind}/{id}/name", ct);
            return json?["name"]?.GetValue<string>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Trả null khi 404 (không có dữ liệu), ném lỗi với các mã khác.</summary>
    private async Task<JsonNode?> GetJsonAsync(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(BaseUrl + path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    internal static IReadOnlyList<MapleItem> ParseItemSearch(JsonNode? json) =>
        json?.AsArray()
            .Where(i => i?["id"] is not null && i["name"] is not null)
            .Select(i => new MapleItem(
                i!["id"]!.GetValue<int>(),
                i["name"]!.GetValue<string>(),
                i["desc"]?.GetValue<string>(),
                i["typeInfo"]?["category"]?.GetValue<string>(),
                i["typeInfo"]?["subCategory"]?.GetValue<string>()))
            .ToList() ?? [];

    internal static MapleItem ParseItemDetail(JsonNode json, bool includeIcon)
    {
        var icon = includeIcon ? json["metaInfo"]?["icon"]?.GetValue<string>() : null;
        return new MapleItem(
            json["id"]!.GetValue<int>(),
            json["description"]?["name"]?.GetValue<string>() ?? "",
            json["description"]?["description"]?.GetValue<string>(),
            json["typeInfo"]?["category"]?.GetValue<string>(),
            json["typeInfo"]?["subCategory"]?.GetValue<string>())
        {
            IconPng = string.IsNullOrEmpty(icon) ? null : Convert.FromBase64String(icon),
        };
    }

    internal static IEnumerable<(string Kind, int Id)> CollectReferences(JsonNode quest)
    {
        foreach (var section in new[] { "requirementToStart", "requirementToComplete" })
        {
            if (quest[section]?["npcId"]?.GetValue<int>() is int npc)
            {
                yield return ("npc", npc);
            }
            foreach (var item in quest[section]?["items"]?.AsArray() ?? [])
            {
                if (item?["id"]?.GetValue<int>() is int itemId) yield return ("item", itemId);
            }
            foreach (var mob in quest[section]?["mobs"]?.AsArray() ?? [])
            {
                if (mob?["id"]?.GetValue<int>() is int mobId) yield return ("mob", mobId);
            }
        }
        foreach (var item in quest["rewardOnComplete"]?["items"]?.AsArray() ?? [])
        {
            if (item?["id"]?.GetValue<int>() is int itemId && (item["count"]?.GetValue<int>() ?? 0) > 0)
            {
                yield return ("item", itemId);
            }
        }
    }

    internal static string BuildFactSheet(JsonNode quest, IReadOnlyDictionary<string, string> names)
    {
        string Name(string kind, int id) => names.GetValueOrDefault($"{kind}:{id}", $"#{id}");

        var sb = new StringBuilder();
        sb.AppendLine($"Quest: {quest["name"]?.GetValue<string>()} (id {quest["id"]})");

        var start = quest["requirementToStart"];
        if (start?["npcId"]?.GetValue<int>() is int startNpc) sb.AppendLine($"Start NPC: {Name("npc", startNpc)}");
        if (start?["levelMinimum"]?.GetValue<int>() is int level) sb.AppendLine($"Min level: {level}");
        if (start?["jobs"] is JsonArray jobs && jobs.Count > 0) sb.AppendLine($"Job ids: {string.Join(", ", jobs)}");

        var complete = quest["requirementToComplete"];
        if (complete?["npcId"]?.GetValue<int>() is int endNpc) sb.AppendLine($"Complete at NPC: {Name("npc", endNpc)}");
        foreach (var item in complete?["items"]?.AsArray() ?? [])
        {
            sb.AppendLine($"Need item: {item?["count"]} x {Name("item", item!["id"]!.GetValue<int>())}");
        }
        foreach (var mob in complete?["mobs"]?.AsArray() ?? [])
        {
            sb.AppendLine($"Kill: {mob?["count"]} x {Name("mob", mob!["id"]!.GetValue<int>())}");
        }

        var reward = quest["rewardOnComplete"];
        if (reward?["exp"] is not null) sb.AppendLine($"Reward EXP: {reward["exp"]}");
        if (reward?["meso"] is not null) sb.AppendLine($"Reward mesos: {reward["meso"]}");
        foreach (var item in reward?["items"]?.AsArray() ?? [])
        {
            var count = item?["count"]?.GetValue<int>() ?? 0;
            if (count > 0) sb.AppendLine($"Reward item: {count} x {Name("item", item!["id"]!.GetValue<int>())}");
        }

        var messages = quest["messages"]?.AsArray().Select(m => m?.GetValue<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        if (messages is { Count: > 0 })
        {
            sb.AppendLine("Quest log text:");
            foreach (var message in messages)
            {
                // #b #k ... là mã màu chữ của game
                sb.AppendLine("- " + message!.Replace("#b", "").Replace("#k", "").Replace("#r", "").Replace("#e", "").Replace("#n", ""));
            }
        }
        return sb.ToString().Trim();
    }
}
