using MapleHelper.Core.Ai;
using MapleHelper.Core.Settings;
using MapleHelper.Infrastructure.Ai;
using MapleHelper.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();

        // Web search có thể mất cả phút, timeout rộng hơn mặc định
        var aiTimeout = TimeSpan.FromSeconds(120);
        services.AddHttpClient<GeminiProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<OpenAiProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<ClaudeProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<DeepSeekProvider>(c => c.Timeout = aiTimeout);

        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        services.AddSingleton<IAiService, AiService>();

        return services;
    }
}
