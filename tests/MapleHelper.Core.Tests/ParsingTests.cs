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

public class NewsHeadlineTests
{
    [Fact]
    public void Headline_splits_title_and_summary_lines()
    {
        Assert.Equal(("Tiêu đề", "Tóm tắt dài"), NewsService.SplitHeadline("Tiêu đề\nTóm tắt\ndài"));
        Assert.Null(NewsService.SplitHeadline("chỉ một dòng"));
    }
}

public class MarkdownMachineTranslationTests
{
    private sealed class UpperTranslator : IMachineTranslator
    {
        public List<string> Requests { get; } = [];

        public Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false)
        {
            Requests.Add(text);
            return Task.FromResult(text.ToUpperInvariant());
        }
    }

    [Fact]
    public async Task Keeps_markdown_prefixes_and_bold_lines_and_batches_requests()
    {
        var translator = new UpperTranslator();
        var markdown = "## While in Debt\n\n**Notice**\n- First item\n1. Step one\nPlain **mixed** text";

        var result = await MarkdownMachineTranslation.TranslateAsync(translator, markdown, TestContext.Current.CancellationToken);

        Assert.Equal("## WHILE IN DEBT\n\n**NOTICE**\n- FIRST ITEM\n1. STEP ONE\nPLAIN MIXED TEXT", result);
        Assert.Single(translator.Requests);
        Assert.DoesNotContain("#", translator.Requests[0]);
        Assert.DoesNotContain("**", translator.Requests[0]);
    }
}

public class GlossaryTranslatorTests
{
    private sealed class TranslateWords : IMachineTranslator
    {
        public string? Sent { get; private set; }

        public Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false)
        {
            Sent = text;
            return Task.FromResult(text.Replace("Welcome to", "Chào mừng đến"));
        }
    }

    [Fact]
    public async Task Proper_nouns_survive_translation_and_longest_term_wins()
    {
        var inner = new TranslateWords();
        var translator = new GlossaryTranslator(inner, ProtectedTerms.NewsGlossary);

        var result = await translator.TranslateAsync("Welcome to MapleStory Classic World, Maplers!", "en", "vi",
            TestContext.Current.CancellationToken);

        Assert.Equal("Chào mừng đến MapleStory Classic World, Maplers!", result);
        Assert.DoesNotContain("Classic", inner.Sent);
    }
}

public class VietnamTimeTests
{
    private static readonly DateTimeOffset October = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Maintenance at 4:00 AM PDT.", "Maintenance at 4:00 AM PDT (18:00 giờ VN).")]
    [InlineData("Ends 11:59 PM PDT", "Ends 11:59 PM PDT (13:59 hôm sau giờ VN)")]
    [InlineData("from 4:00 AM - 10:00 AM PDT", "from 4:00 AM - 10:00 AM PDT (18:00 – 00:00 hôm sau giờ VN)")]
    [InlineData("at 7:00 PM PST", "at 7:00 PM PST (10:00 hôm sau giờ VN)")]
    [InlineData("at 23:00 UTC", "at 23:00 UTC (06:00 hôm sau giờ VN)")]
    public void Adds_vietnam_time(string input, string expected) =>
        Assert.Equal(expected, VietnamTime.Annotate(input, October));

    [Fact]
    public void Skips_utc_note_inside_parentheses_and_bare_numbers()
    {
        Assert.Equal("4:00 AM PDT (18:00 giờ VN) (11:00 UTC)", VietnamTime.Annotate("4:00 AM PDT (11:00 UTC)", October));
        Assert.Equal("Season 2 UTC", VietnamTime.Annotate("Season 2 UTC", October));
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
