using MapleHelper.Core.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.Infrastructure.Ai;

/// <summary>
/// Resolve provider mỗi lần gọi thay vì giữ instance, để typed HttpClient của IHttpClientFactory
/// được xoay vòng handler đúng cách.
/// </summary>
public sealed class AiProviderRegistry(IServiceProvider services) : IAiProviderRegistry
{
    private static readonly Dictionary<AiProviderKind, Type> ProviderTypes = new()
    {
        [AiProviderKind.Gemini] = typeof(GeminiProvider),
        [AiProviderKind.OpenAi] = typeof(OpenAiProvider),
        [AiProviderKind.Claude] = typeof(ClaudeProvider),
        [AiProviderKind.DeepSeek] = typeof(DeepSeekProvider),
    };

    public IReadOnlyList<IAiProvider> All => ProviderTypes.Keys.Select(Get).ToList();

    public IAiProvider Get(AiProviderKind kind) =>
        (IAiProvider)services.GetRequiredService(ProviderTypes[kind]);
}
