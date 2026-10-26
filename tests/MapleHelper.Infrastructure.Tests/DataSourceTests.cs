using System.Text.Json.Nodes;
using MapleHelper.Core.News;
using MapleHelper.Infrastructure.Ai;
using MapleHelper.Infrastructure.MapleData;
using MapleHelper.Infrastructure.News;

namespace MapleHelper.Infrastructure.Tests;

public class NexonNewsSourceTests
{
    [Fact]
    public void Parses_feed_items_with_absolute_images_and_classic_flag()
    {
        var json = JsonNode.Parse("""
            [{"imageThumbnail":"/media/qedhelhq/banner.jpg","summary":"Click here!","featured":false,"isMSCW":true,
              "liveDate":"2026-10-22T14:00:07Z","name":"Introducing the Debt System","id":47102,"category":"general"},
             {"imageThumbnail":null,"summary":"x","featured":true,"isMSCW":false,
              "liveDate":"2026-10-23T09:00:00Z","name":"v.271 Known Issues","id":45226,"category":"maintenance"}]
            """)!;

        var articles = NexonNewsSource.ParseList(json);

        Assert.Equal(2, articles.Count);
        var debt = articles[0];
        Assert.Equal(47102, debt.Id);
        Assert.True(debt.IsClassicWorld);
        Assert.Equal(NewsCategory.General, debt.Category);
        Assert.Equal("https://g.nexonstatic.com/media/qedhelhq/banner.jpg", debt.ImageUrl);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 14, 0, 7, TimeSpan.Zero), debt.LiveDate);
        Assert.Null(articles[1].ImageUrl);
        Assert.Equal(NewsCategory.Maintenance, articles[1].Category);
    }

    [Theory]
    [InlineData("sale", NewsCategory.Sale)]
    [InlineData("EVENTS", NewsCategory.Events)]
    [InlineData("something-new", NewsCategory.Other)]
    [InlineData(null, NewsCategory.Other)]
    public void Maps_categories(string? raw, NewsCategory expected) => Assert.Equal(expected, NexonNewsSource.ParseCategory(raw));
}

public class MapleIoClientTests
{
    private static readonly JsonNode Quest = JsonNode.Parse("""
        {"id":2009,"name":"Pia and the Blue Mushroom",
         "messages":["Let's start looking for Pia in Henesys","She wanted #b60 blue mushroom caps#k"],
         "requirementToStart":{"levelMinimum":20,"npcId":1012102},
         "requirementToComplete":{"items":[{"count":40,"id":4000009}],"npcId":1012102},
         "rewardOnComplete":{"exp":3000,"items":[{"id":2040000,"count":1},{"id":4000009,"count":-40}]}}
        """)!;

    [Fact]
    public void Collects_npc_item_references_without_consumed_items()
    {
        var refs = MapleIoClient.CollectReferences(Quest).Distinct().ToList();

        Assert.Contains(("npc", 1012102), refs);
        Assert.Contains(("item", 4000009), refs);
        Assert.Contains(("item", 2040000), refs);
    }

    [Fact]
    public void Fact_sheet_uses_resolved_names_and_strips_color_codes()
    {
        var names = new Dictionary<string, string>
        {
            ["npc:1012102"] = "Pia",
            ["item:4000009"] = "Blue Mushroom Cap",
            ["item:2040000"] = "Scroll for Helmet for DEF",
        };

        var sheet = MapleIoClient.BuildFactSheet(Quest, names);

        Assert.Contains("Start NPC: Pia", sheet);
        Assert.Contains("Min level: 20", sheet);
        Assert.Contains("Need item: 40 x Blue Mushroom Cap", sheet);
        Assert.Contains("Reward EXP: 3000", sheet);
        Assert.Contains("Reward item: 1 x Scroll for Helmet for DEF", sheet);
        Assert.DoesNotContain("Reward item: -40", sheet);
        Assert.DoesNotContain("#b", sheet);
    }

    [Fact]
    public void Parses_item_search_results()
    {
        var json = JsonNode.Parse("""
            [{"isCash":false,"name":"Orange Mushroom Cap","desc":"A cap","id":4000001,
              "typeInfo":{"overallCategory":"Etc","category":"Other","subCategory":"Monster Drop"}}]
            """);

        var item = Assert.Single(MapleIoClient.ParseItemSearch(json));

        Assert.Equal(4000001, item.Id);
        Assert.Equal("Monster Drop", item.SubCategory);
    }
}

public class GeminiModelTests
{
    private readonly GeminiProvider _provider = new(new HttpClient());

    [Fact]
    public void Prefers_latest_alias_when_available()
    {
        Assert.Equal("gemini-flash-latest", _provider.PickPreferredModel(["gemini-3.8-flash", "gemini-flash-latest"]));
    }

    [Fact]
    public void Picks_highest_stable_flash_ignoring_lite_and_image()
    {
        string[] models = ["gemini-2.5-flash", "gemini-3.8-flash-lite", "gemini-3.8-flash-image", "gemini-3.8-flash-preview-09", "gemini-3.8-flash", "gemini-3.8-pro"];

        Assert.Equal("gemini-3.8-flash", _provider.PickPreferredModel(models));
    }

    [Theory]
    [InlineData("gemini-2.5-flash", "thinkingBudget")]
    [InlineData("gemini-3.8-flash", "thinkingLevel")]
    [InlineData("gemini-flash-latest", "thinkingLevel")]
    [InlineData("gemini-2.5-pro", null)]
    public void Thinking_config_depends_on_model_family(string model, string? expectedKey)
    {
        var config = GeminiProvider.BuildThinkingConfig(model);
        if (expectedKey is null)
        {
            Assert.Null(config);
        }
        else
        {
            Assert.True(config!.ContainsKey(expectedKey));
        }
    }
}
