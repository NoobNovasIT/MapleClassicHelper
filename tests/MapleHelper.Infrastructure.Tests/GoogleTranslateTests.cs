using System.Net;
using System.Text;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Infrastructure.Translation;

namespace MapleHelper.Infrastructure.Tests;

/// <summary>
/// Trạng thái tạm chặn của client là static, nên mọi test dùng GoogleTranslateClient nằm trong class này
/// (xunit chạy tuần tự trong một class) và reset trước mỗi test.
/// </summary>
public sealed class GoogleTranslateTests
{
    private const string GtxHello = """[[["hello","xin chào",null,null,10]],null,"vi"]""";

    public GoogleTranslateTests() => GoogleTranslateClient.ResetCooldowns();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Joins_all_sentence_segments()
    {
        var body = """[[["Anyone going to Zakum? ","ae ơi có ai đi Zakum không? ",null,null,10],["I need 2 people","mình cần 2 người",null,null,10]],null,"vi"]""";

        Assert.Equal("Anyone going to Zakum? I need 2 people", GoogleTranslateClient.ParseResponse(body));
    }

    [Theory]
    [InlineData("""["Xin chào.\nDòng hai."]""", "Xin chào.\nDòng hai.")]
    [InlineData("""[["Mang cho tôi 10 cái mũ.","en"]]""", "Mang cho tôi 10 cái mũ.")]
    public void Parses_fallback_endpoint_formats(string body, string expected) =>
        Assert.Equal(expected, GoogleTranslateClient.ParseResponse(body, GoogleTranslateClient.ResponseFormat.DictChrome));

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    public void Bad_response_throws_friendly_error(string body) =>
        Assert.Throws<MachineTranslationException>(() => GoogleTranslateClient.ParseResponse(body));

    [Fact]
    public async Task Sends_text_in_form_body_with_languages_in_query()
    {
        var handler = new ScriptedHandler((HttpStatusCode.OK, GtxHello));
        var client = new GoogleTranslateClient(new HttpClient(handler));

        var result = await client.TranslateAsync("xin chào", "vi", "en", Ct);

        Assert.Equal("hello", result);
        var (url, body) = Assert.Single(handler.Requests);
        Assert.Contains("sl=vi", url);
        Assert.Contains("tl=en", url);
        Assert.Contains("q=xin+ch%C3%A0o", body);
    }

    [Fact]
    public async Task Rate_limited_primary_falls_back_and_stays_skipped()
    {
        var handler = new ScriptedHandler(
            (HttpStatusCode.TooManyRequests, ""),
            (HttpStatusCode.OK, """["hello"]"""),
            (HttpStatusCode.OK, """["again"]"""));
        var client = new GoogleTranslateClient(new HttpClient(handler));

        Assert.Equal("hello", await client.TranslateAsync("xin chào", "vi", "en", Ct));
        Assert.Equal("again", await client.TranslateAsync("lần nữa", "vi", "en", Ct));

        Assert.Contains("translate.googleapis.com", handler.Requests[0].Url);
        Assert.Contains("clients5.google.com", handler.Requests[1].Url);
        Assert.Contains("clients5.google.com", handler.Requests[2].Url); // endpoint chính đang tạm nghỉ
    }

    [Fact]
    public async Task Background_requests_never_use_fallback()
    {
        var handler = new ScriptedHandler((HttpStatusCode.TooManyRequests, ""));
        var client = new GoogleTranslateClient(new HttpClient(handler));

        await Assert.ThrowsAsync<MachineTranslationRateLimitedException>(() =>
            client.TranslateAsync("news", "en", "vi", Ct, background: true));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Both_endpoints_limited_gives_clear_error()
    {
        var handler = new ScriptedHandler((HttpStatusCode.TooManyRequests, ""));
        var client = new GoogleTranslateClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<MachineTranslationRateLimitedException>(() => client.TranslateAsync("hi", "vi", "en", Ct));

        Assert.Contains("5 phút", ex.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Trả lần lượt các phản hồi cho trước; hết danh sách thì lặp lại phản hồi cuối.</summary>
    private sealed class ScriptedHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(ct)));
            var (status, body) = responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
