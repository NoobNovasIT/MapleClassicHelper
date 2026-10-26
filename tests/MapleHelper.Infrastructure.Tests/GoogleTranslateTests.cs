using System.Net;
using System.Text;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Infrastructure.Translation;

namespace MapleHelper.Infrastructure.Tests;

public class GoogleTranslateTests
{
    [Fact]
    public void Joins_all_sentence_segments()
    {
        var body = """[[["Anyone going to Zakum? ","ae ơi có ai đi Zakum không? ",null,null,10],["I need 2 people","mình cần 2 người",null,null,10]],null,"vi"]""";

        Assert.Equal("Anyone going to Zakum? I need 2 people", GoogleTranslateClient.ParseResponse(body));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    public void Bad_response_throws_friendly_error(string body) =>
        Assert.Throws<MachineTranslationException>(() => GoogleTranslateClient.ParseResponse(body));

    [Fact]
    public async Task Sends_text_in_form_body_with_languages_in_query()
    {
        var handler = new CaptureHandler("""[[["hello","xin chào",null,null,10]],null,"vi"]""");
        var client = new GoogleTranslateClient(new HttpClient(handler));

        var result = await client.TranslateAsync("xin chào", "vi", "en", TestContext.Current.CancellationToken);

        Assert.Equal("hello", result);
        Assert.Contains("sl=vi", handler.Url);
        Assert.Contains("tl=en", handler.Url);
        Assert.Contains("q=xin+ch%C3%A0o", handler.Body);
    }

    private sealed class CaptureHandler(string response) : HttpMessageHandler
    {
        public string Url { get; private set; } = "";
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
