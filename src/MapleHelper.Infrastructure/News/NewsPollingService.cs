using MapleHelper.Core.Ai;
using MapleHelper.Core.News;
using MapleHelper.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MapleHelper.Infrastructure.News;

/// <summary>Chạy nền: lấy tin mới định kỳ rồi dịch tiêu đề/tóm tắt các bài chưa dịch.</summary>
public sealed class NewsPollingService(
    INewsService news,
    ISettingsService settings,
    ILogger<NewsPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Để UI lên trước rồi mới tải
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var added = await news.RefreshAsync(stoppingToken);
                logger.LogInformation("Đã lấy tin Nexon: {Added} bài mới", added);
                await news.TranslatePendingAsync(stoppingToken);
            }
            catch (AiException ex) when (ex.Kind == AiErrorKind.MissingApiKey)
            {
                logger.LogInformation("Chưa có API key, bỏ qua dịch tin");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Lấy hoặc dịch tin thất bại");
            }

            var minutes = Math.Clamp(settings.Current.News.PollIntervalMinutes, 5, 24 * 60);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }
}
