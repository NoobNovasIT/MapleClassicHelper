using System.IO;
using System.Windows;
using MapleHelper.App.Bootstrap;
using MapleHelper.App.Services;
using MapleHelper.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Velopack;

namespace MapleHelper.App;

public static class Program
{
    private const string InstanceName = "MapleClassicHelper";

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack phải chạy đầu tiên: xử lý các hook install/update/uninstall rồi thoát sớm nếu cần
        VelopackApp.Build().Run();

        using var instance = SingleInstance.TryAcquire(InstanceName);
        if (instance is null)
        {
            SingleInstance.SignalExisting(InstanceName);
            return 0;
        }

        var paths = AppPaths.Default();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.LogsDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Khởi động Maple Classic Helper");

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddSerilog();
            builder.Services.AddAppServices(paths);
            using var host = builder.Build();

            var app = new App(host, instance, startMinimized: args.Contains("--minimized"));
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Ứng dụng dừng do lỗi không xử lý được");
            MessageBox.Show($"Ứng dụng gặp lỗi và phải đóng:\n{ex.Message}", "Maple Classic Helper",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
