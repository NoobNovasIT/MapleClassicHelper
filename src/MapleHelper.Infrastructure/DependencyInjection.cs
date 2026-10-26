using MapleHelper.Core.Ai;
using MapleHelper.Core.Features.Lookup;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.History;
using MapleHelper.Core.MapleData;
using MapleHelper.Core.News;
using MapleHelper.Core.Settings;
using MapleHelper.Infrastructure.Ai;
using MapleHelper.Infrastructure.MapleData;
using MapleHelper.Infrastructure.News;
using MapleHelper.Infrastructure.Persistence;
using MapleHelper.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.Infrastructure;

public static class DependencyInjection
{
    private const string UserAgent = "MapleClassicHelper/0.1 (+https://github.com)";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();

        // AI: web search và dịch bài dài có thể mất vài phút
        var aiTimeout = TimeSpan.FromSeconds(240);
        services.AddHttpClient<GeminiProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<OpenAiProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<ClaudeProvider>(c => c.Timeout = aiTimeout);
        services.AddHttpClient<DeepSeekProvider>(c => c.Timeout = aiTimeout);
        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        services.AddSingleton<IAiService, AiService>();

        // Lưu trữ
        services.AddSingleton<AppDatabase>();
        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<INewsRepository, NewsRepository>();

        // Nguồn dữ liệu ngoài
        services.AddHttpClient<INewsSource, NexonNewsSource>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });
        services.AddHttpClient<IMapleDataClient, MapleIoClient>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        });

        // Tính năng
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddSingleton<INewsService, NewsService>();
        services.AddSingleton<IItemLookupService, ItemLookupService>();
        services.AddSingleton<IQuestGuideService, QuestGuideService>();
        services.AddHostedService<NewsPollingService>();

        return services;
    }
}
