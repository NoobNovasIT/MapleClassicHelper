using MapleHelper.Core.Ai;
using MapleHelper.Core.Settings;

namespace MapleHelper.Core.Tests;

public class AiServiceTests
{
    private readonly FakeProvider _provider = new(AiProviderKind.Gemini, AiCapabilities.Vision);
    private readonly FakeSettings _settings = new();
    private readonly FakeSecrets _secrets = new();

    private AiService CreateService() => new(new FakeRegistry(_provider), _settings, _secrets);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Throws_MissingApiKey_when_no_key_saved()
    {
        var ex = await Assert.ThrowsAsync<AiException>(() => CreateService().GenerateAsync(new AiRequest { Prompt = "hi" }, Ct));
        Assert.Equal(AiErrorKind.MissingApiKey, ex.Kind);
    }

    [Fact]
    public async Task Uses_default_model_when_none_selected()
    {
        _secrets.Set(SecretKeys.ApiKey(AiProviderKind.Gemini), "key-1");

        await CreateService().GenerateAsync(new AiRequest { Prompt = "hi" }, Ct);

        Assert.Equal(new AiConnection("key-1", "default-model"), _provider.LastConnection);
    }

    [Fact]
    public async Task Uses_model_saved_in_settings()
    {
        _secrets.Set(SecretKeys.ApiKey(AiProviderKind.Gemini), "key-1");
        _settings.Current.Ai.Models[AiProviderKind.Gemini] = "custom-model";

        await CreateService().GenerateAsync(new AiRequest { Prompt = "hi" }, Ct);

        Assert.Equal("custom-model", _provider.LastConnection?.Model);
    }

    [Fact]
    public async Task Rejects_image_for_text_only_provider_before_calling_it()
    {
        var textOnly = new FakeProvider(AiProviderKind.Gemini, AiCapabilities.None);
        _secrets.Set(SecretKeys.ApiKey(AiProviderKind.Gemini), "key-1");
        var service = new AiService(new FakeRegistry(textOnly), _settings, _secrets);

        var ex = await Assert.ThrowsAsync<AiException>(() =>
            service.GenerateAsync(new AiRequest { Prompt = "hi", ImagePng = [1, 2, 3] }, Ct));

        Assert.Equal(AiErrorKind.NotSupported, ex.Kind);
        Assert.Null(textOnly.LastConnection);
    }

    [Fact]
    public async Task Retired_model_is_replaced_saved_and_retried()
    {
        _secrets.Set(SecretKeys.ApiKey(AiProviderKind.Gemini), "key-1");
        _settings.Current.Ai.Models[AiProviderKind.Gemini] = "retired-model";
        _provider.MissingModels.Add("retired-model");
        _provider.Available = ["new-model", "other"];

        var response = await CreateService().GenerateAsync(new AiRequest { Prompt = "hi" }, Ct);

        Assert.Equal("ok", response.Text);
        Assert.Equal("new-model", _provider.LastConnection?.Model);
        Assert.Equal("new-model", _settings.Current.Ai.GetModel(AiProviderKind.Gemini));
        Assert.Equal(1, _settings.SaveCount);
    }

    [Fact]
    public async Task Model_not_found_without_replacement_rethrows()
    {
        _secrets.Set(SecretKeys.ApiKey(AiProviderKind.Gemini), "key-1");
        _provider.MissingModels.Add("default-model");
        _provider.Available = [];

        var ex = await Assert.ThrowsAsync<AiException>(() => CreateService().GenerateAsync(new AiRequest { Prompt = "hi" }, Ct));

        Assert.Equal(AiErrorKind.ModelNotFound, ex.Kind);
    }

    private sealed class FakeProvider(AiProviderKind kind, AiCapabilities capabilities) : IAiProvider
    {
        public AiConnection? LastConnection { get; private set; }
        public HashSet<string> MissingModels { get; } = [];
        public IReadOnlyList<string> Available { get; set; } = [];

        public AiProviderKind Kind => kind;
        public string DisplayName => "Fake";
        public AiCapabilities Capabilities => capabilities;
        public string DefaultModel => "default-model";
        public IReadOnlyList<string> SuggestedModels => [];
        public string ApiKeyUrl => "https://example.com";

        public Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken ct = default) =>
            Task.FromResult(Available);

        public Task<AiResponse> GenerateAsync(AiConnection connection, AiRequest request, CancellationToken ct = default)
        {
            if (MissingModels.Contains(connection.Model))
            {
                throw new AiException(AiErrorKind.ModelNotFound, "gone");
            }
            LastConnection = connection;
            return Task.FromResult(AiResponse.FromText("ok"));
        }

        public string? PickPreferredModel(IReadOnlyList<string> available) => available.FirstOrDefault();
    }

    private sealed class FakeRegistry(IAiProvider provider) : IAiProviderRegistry
    {
        public IReadOnlyList<IAiProvider> All => [provider];
        public IAiProvider Get(AiProviderKind kind) => provider;
    }

    internal sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public int SaveCount { get; private set; }
        public event EventHandler? Changed;
        public Task SaveAsync(CancellationToken ct = default)
        {
            SaveCount++;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string key) => _values.GetValueOrDefault(key);
        public void Set(string key, string value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
    }
}
