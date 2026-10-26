using System.Globalization;
using System.Text.Json.Nodes;
using MapleHelper.Core.News;
using MapleHelper.Core.Settings;

namespace MapleHelper.Infrastructure.News;

/// <summary>
/// Đọc JSON CMS mà trang nexon.com/maplestory/news dùng (trang đó render phía client nên HTML không có bài viết).
/// List: GET {FeedUrl} → mảng {id, name, summary, category, liveDate, imageThumbnail, featured, isMSCW}.
/// Chi tiết: GET {FeedUrl}/{id} → thêm "body" là HTML.
/// </summary>
public sealed class NexonNewsSource(HttpClient http, ISettingsService settings) : INewsSource
{
    private const string MediaHost = "https://g.nexonstatic.com";

    public async Task<IReadOnlyList<NewsArticle>> FetchListAsync(CancellationToken ct = default)
    {
        var json = await GetJsonAsync(settings.Current.News.FeedUrl, ct);
        return ParseList(json);
    }

    public async Task<string> FetchBodyHtmlAsync(long id, CancellationToken ct = default)
    {
        var json = await GetJsonAsync($"{settings.Current.News.FeedUrl.TrimEnd('/')}/{id}", ct);
        return json["body"]?.GetValue<string>() ?? "";
    }

    private async Task<JsonNode> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("Nexon trả về dữ liệu rỗng.");
    }

    internal static IReadOnlyList<NewsArticle> ParseList(JsonNode json)
    {
        var articles = new List<NewsArticle>();
        foreach (var item in json.AsArray())
        {
            if (item?["id"] is null || item["name"] is null)
            {
                continue;
            }
            articles.Add(new NewsArticle
            {
                Id = item["id"]!.GetValue<long>(),
                Title = item["name"]!.GetValue<string>().Trim(),
                Summary = item["summary"]?.GetValue<string>()?.Trim() ?? "",
                Category = ParseCategory(item["category"]?.GetValue<string>()),
                LiveDate = DateTimeOffset.TryParse(item["liveDate"]?.GetValue<string>(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.UtcNow,
                ImageUrl = ToAbsoluteImage(item["imageThumbnail"]?.GetValue<string>()),
                Featured = item["featured"]?.GetValue<bool>() ?? false,
                IsClassicWorld = item["isMSCW"]?.GetValue<bool>() ?? false,
            });
        }
        return articles;
    }

    internal static NewsCategory ParseCategory(string? category) => category?.ToLowerInvariant() switch
    {
        "general" => NewsCategory.General,
        "update" => NewsCategory.Update,
        "sale" => NewsCategory.Sale,
        "events" => NewsCategory.Events,
        "community" => NewsCategory.Community,
        "maintenance" => NewsCategory.Maintenance,
        _ => NewsCategory.Other,
    };

    private static string? ToAbsoluteImage(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null
        : path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path
        : MediaHost + (path.StartsWith('/') ? path : "/" + path);
}
