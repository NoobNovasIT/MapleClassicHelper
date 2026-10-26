using MapleHelper.App.Features.Settings;
using MapleHelper.App.Services;
using MapleHelper.App.Shell;
using MapleHelper.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MapleHelper.App.Bootstrap;

public static class ServiceRegistration
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, AppPaths paths)
    {
        services.AddInfrastructure(paths);

        services.AddSingleton<TrayIconService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services;
    }
}
