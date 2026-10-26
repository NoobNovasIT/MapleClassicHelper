using MapleHelper.Core.Ai;
using MapleHelper.Core.Features.Lookup;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.MapleData;
using MapleHelper.Core.News;

namespace MapleHelper.Core.Tests;

public class AiJsonTests
{
    [Fact]
    public void Extracts_json_from_code_fence_and_prose()
    {
        var node = AiJson.TryParse("Đây là kết quả:\n```json\n{\"name\": \"Red Potion\"}\n```");
        Assert.Equal("Red Potion", node.GetString("name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("không có json")]
    [InlineData("{ hỏng")]
    public void Returns_null_for_non_json(string text) => Assert.Null(AiJson.TryParse(text));
}

public class TranslationParsingTests
{
    [Fact]
    public void Screen_translation_reads_json_fields()
    {
        var result = TranslationService.ParseScreenTranslation("""{"source": "Bring me 10 caps", "translation": "Mang cho ta 10 mũ"}""");
        Assert.Equal("Bring me 10 caps", result.SourceText);
        Assert.Equal("Mang cho ta 10 mũ", result.Translation);
    }

    [Fact]
    public void Screen_translation_falls_back_to_raw_text()
    {
        var result = TranslationService.ParseScreenTranslation("Mang cho ta 10 mũ");
        Assert.Equal("", result.SourceText);
        Assert.Equal("Mang cho ta 10 mũ", result.Translation);
    }

    [Theory]
    [InlineData("\"anyone pt?\"", "anyone pt?")]
    [InlineData("  lf party  ", "lf party")]
    public void Chat_output_is_unquoted_and_trimmed(string raw, string expected) =>
        Assert.Equal(expected, TranslationService.CleanChatOutput(raw));

    [Fact]
    public void Item_name_is_read_from_json()
    {
        Assert.Equal("Orange Mushroom Cap", ItemLookupService.ReadName("""{"name": " Orange Mushroom Cap "}"""));
        Assert.Equal("", ItemLookupService.ReadName("xin lỗi"));
    }
}

public class NewsServiceParsingTests
{
    [Fact]
    public void List_translation_keeps_only_expected_ids_with_title()
    {
        var text = """
            [{"id": 1, "title": "Tin một", "summary": "Tóm tắt"},
             {"id": 2, "title": ""},
             {"id": 99, "title": "Lạ"}]
            """;

        var results = NewsService.ParseListTranslation(text, new HashSet<long> { 1, 2 });

        Assert.Equal([(1L, "Tin một", "Tóm tắt")], results);
    }

    [Fact]
    public void Split_chunks_respects_paragraphs_and_limit()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => new string((char)('a' + i), 30)));

        var chunks = NewsService.SplitChunks(text, 70).ToList();

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 70 + 32));
        Assert.Equal(text.Replace("\n", ""), string.Concat(chunks).Replace("\n", ""));
    }
}

public class HtmlTextTests
{
    [Fact]
    public void Converts_headings_lists_bold_and_entities()
    {
        var html = """
            <div class="cms-ms"><h2>While in Debt</h2>
            <p>Hi <strong>Classic</strong> Maplers &amp; friends,</p>
            <ul><li>First</li><li>Second</li></ul>
            <script>alert(1)</script></div>
            """;

        var markdown = HtmlText.ToMarkdown(html);

        Assert.Contains("## While in Debt", markdown);
        Assert.Contains("Hi **Classic** Maplers & friends,", markdown);
        Assert.Contains("- First", markdown);
        Assert.Contains("- Second", markdown);
        Assert.DoesNotContain("alert", markdown);
        Assert.DoesNotContain("<", markdown);
    }
}

public class NewsArticleTests
{
    [Fact]
    public void Url_uses_category_id_and_slug()
    {
        var article = new NewsArticle { Id = 47102, Category = NewsCategory.General, Title = "Introducing the Debt System!" };
        Assert.Equal("https://www.nexon.com/maplestory/news/general/47102/introducing-the-debt-system", article.Url);
    }
}

public class MapleNameMatchTests
{
    private sealed record Named(string Name);

    [Fact]
    public void Prefers_exact_then_prefix_then_contains()
    {
        Named[] items = [new("V.I. Orange Mushroom Wine"), new("Orange Mushroom Cap Box"), new("Orange Mushroom Cap")];

        Assert.Equal("Orange Mushroom Cap", MapleNameMatch.Best(items, "orange mushroom cap", n => n.Name)!.Name);
        Assert.Equal("Orange Mushroom Cap Box", MapleNameMatch.Best(items[..2], "Orange Mushroom", n => n.Name)!.Name);
        Assert.Null(MapleNameMatch.Best(Array.Empty<Named>(), "x", n => n.Name));
    }
}
