using System.Net;
using System.Text;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.Settings;
using MapleHelper.Infrastructure.Translation;

namespace MapleHelper.Infrastructure.Tests;

[Collection(GoogleTranslateTests.SharedState)]
public sealed class AzureTranslatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_key_region_and_parses_translation()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """[{"translations":[{"text":"Let me join the party","to":"en"}]}]""");
        var client = new AzureTranslatorClient(new HttpClient(handler), new MemorySecrets(), new Settings());

        var result = await client.TranslateWithKeyAsync("key-123", "southeastasia", "cho tôi vào tổ đội với", "vi", "en", Ct);

        Assert.Equal("Let me join the party", result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("key-123", request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Equal("southeastasia", request.Headers.GetValues("Ocp-Apim-Subscription-Region").Single());
        Assert.Contains("from=vi", request.RequestUri!.Query);
        Assert.Contains("to=en", request.RequestUri.Query);
    }

    [Fact]
    public async Task Auto_language_omits_from_and_blank_region_omits_header()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """[{"detectedLanguage":{"language":"en"},"translations":[{"text":"Xin chào","to":"vi"}]}]""");
        var client = new AzureTranslatorClient(new HttpClient(handler), new MemorySecrets(), new Settings());

        await client.TranslateWithKeyAsync("k", "", "Hello", "auto", "vi", Ct);

        var request = Assert.Single(handler.Requests);
        Assert.DoesNotContain("from=", request.RequestUri!.Query);
        Assert.False(request.Headers.Contains("Ocp-Apim-Subscription-Region"));
    }

    [Fact]
    public void Maps_quota_and_auth_errors()
    {
        Assert.IsType<MachineTranslationRateLimitedException>(
            AzureTranslatorClient.MapError(HttpStatusCode.Forbidden, """{"error":{"code":403001,"message":"quota"}}"""));
        Assert.Contains("không hợp lệ", AzureTranslatorClient.MapError(HttpStatusCode.Unauthorized, "").Message);
    }

    [Fact]
    public async Task Fallback_uses_azure_only_for_interactive_requests_when_google_fails()
    {
        GoogleTranslateClient.ResetCooldowns();
        var secrets = new MemorySecrets();
        secrets.Set(SecretKeys.AzureTranslatorKey, "k");
        var google = new GoogleTranslateClient(new HttpClient(new RecordingHandler(HttpStatusCode.TooManyRequests, "")));
        var azureHandler = new RecordingHandler(HttpStatusCode.OK, """[{"translations":[{"text":"hello","to":"en"}]}]""");
        var azure = new AzureTranslatorClient(new HttpClient(azureHandler), secrets, new Settings());
        var translator = new FallbackMachineTranslator(google, azure);

        Assert.Equal("hello", await translator.TranslateAsync("xin chào", "vi", "en", Ct));
        GoogleTranslateClient.ResetCooldowns();
        await Assert.ThrowsAnyAsync<MachineTranslationException>(() => translator.TranslateAsync("tin", "en", "vi", Ct, background: true));

        Assert.Single(azureHandler.Requests);
        GoogleTranslateClient.ResetCooldowns();
    }

    [Fact]
    public async Task Fallback_without_azure_key_explains_how_to_fix()
    {
        GoogleTranslateClient.ResetCooldowns();
        var google = new GoogleTranslateClient(new HttpClient(new RecordingHandler(HttpStatusCode.TooManyRequests, "")));
        var azure = new AzureTranslatorClient(new HttpClient(new RecordingHandler(HttpStatusCode.OK, "")), new MemorySecrets(), new Settings());

        var ex = await Assert.ThrowsAnyAsync<MachineTranslationException>(() =>
            new FallbackMachineTranslator(google, azure).TranslateAsync("hi", "vi", "en", Ct));

        Assert.Contains("Azure Translator", ex.Message);
        GoogleTranslateClient.ResetCooldowns();
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class MemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string key) => _values.GetValueOrDefault(key);
        public void Set(string key, string value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
    }

    private sealed class Settings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public event EventHandler? Changed;
        public Task SaveAsync(CancellationToken ct = default)
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
