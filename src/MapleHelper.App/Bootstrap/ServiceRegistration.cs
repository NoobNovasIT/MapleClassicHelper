using MapleHelper.App.Features.ChatTranslate;
using MapleHelper.App.Features.History;
using MapleHelper.App.Features.News;
using MapleHelper.App.Features.Overlay;
using MapleHelper.App.Features.Results;
using MapleHelper.App.Features.Saved;
using MapleHelper.App.Features.Settings;
using MapleHelper.App.Features.Snipping;
using MapleHelper.App.Services;
using MapleHelper.App.Shell;
using MapleHelper.Core.Hotkeys;
using MapleHelper.Core.Updates;
using MapleHelper.Infrastructure;
using MapleHelper.Native;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.App.Bootstrap;

public static class ServiceRegistration
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, AppPaths paths)
    {
        services.AddInfrastructure(paths);

        // Native: GlobalHotkeyService tạo HWND nên phải resolve lần đầu trên UI thread (App.OnStartup)
        services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<SnippingService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();

        // Overlay
        services.AddSingleton<OverlayCoordinator>();
        services.AddSingleton<ChatTranslateViewModel>();
        services.AddSingleton<ChatTranslateWindow>();
        services.AddTransient<ResultPopupViewModel>();

        // Cửa sổ chính
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<NewsViewModel>();
        services.AddSingleton<SavedViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<HotkeySettingsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services;
    }
}
