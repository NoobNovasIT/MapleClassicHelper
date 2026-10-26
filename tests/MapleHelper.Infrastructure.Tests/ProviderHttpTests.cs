using System.Net;
using System.Text;
using MapleHelper.Core.Ai;
using MapleHelper.Infrastructure.Ai;

namespace MapleHelper.Infrastructure.Tests;

public class ProviderHttpTests
{
    private const string OkBody = """{"choices":[{"finish_reason":"stop","message":{"content":"OK"}}]}""";

    [Fact]
    public async Task Retries_on_429_then_succeeds()
    {
        var handler = new StubHandler(
            (HttpStatusCode.TooManyRequests, """{"error":{"message":"slow down"}}"""),
            (HttpStatusCode.OK, OkBody));
        var provider = new DeepSeekProvider(new HttpClient(handler));

        var response = await provider.GenerateAsync(new AiConnection("k", "deepseek-chat"), new AiRequest { Prompt = "hi" },
            TestContext.Current.CancellationToken);

        Assert.Equal("OK", response.Text);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiErrorKind.InvalidApiKey)]
    [InlineData(HttpStatusCode.NotFound, AiErrorKind.ModelNotFound)]
    [InlineData(HttpStatusCode.PaymentRequired, AiErrorKind.QuotaExceeded)]
    public async Task Maps_http_errors_without_retry(HttpStatusCode status, AiErrorKind expected)
    {
        var handler = new StubHandler((status, """{"error":{"message":"nope"}}"""));
        var provider = new DeepSeekProvider(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<AiException>(() =>
            provider.GenerateAsync(new AiConnection("k", "m"), new AiRequest { Prompt = "hi" }, TestContext.Current.CancellationToken));

        Assert.Equal(expected, ex.Kind);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Gemini_invalid_key_400_maps_to_InvalidApiKey()
    {
        var handler = new StubHandler((HttpStatusCode.BadRequest,
            """{"error":{"code":400,"message":"API key not valid.","status":"INVALID_ARGUMENT","details":[{"reason":"API_KEY_INVALID"}]}}"""));
        var provider = new GeminiProvider(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<AiException>(() => provider.ListModelsAsync("bad", TestContext.Current.CancellationToken));

        Assert.Equal(AiErrorKind.InvalidApiKey, ex.Kind);
    }

    [Fact]
    public async Task Claude_sends_required_headers()
    {
        var handler = new StubHandler((HttpStatusCode.OK, """{"stop_reason":"end_turn","content":[{"type":"text","text":"OK"}]}"""));
        var provider = new ClaudeProvider(new HttpClient(handler));

        await provider.GenerateAsync(new AiConnection("sk-test", "claude-opus-5-5"), new AiRequest { Prompt = "hi" },
            TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("sk-test", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
        Assert.Equal("server-side-fallback-2026-07-01", request.Headers.GetValues("anthropic-beta").Single());
    }

    [Fact]
    public void Extracts_error_message_from_json_or_truncates_raw_body()
    {
        Assert.Equal("bad key", AiProviderBase.ExtractErrorMessage("""{"error":{"message":"bad key"}}"""));
        Assert.Equal(300, AiProviderBase.ExtractErrorMessage(new string('x', 1000)).Length);
    }

    private sealed class StubHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var (status, body) = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
