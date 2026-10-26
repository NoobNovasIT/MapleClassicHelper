using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MapleHelper.App.Services;
using MapleHelper.App.Shell;
using MapleHelper.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly SingleInstance _instance;
    private readonly bool _startMinimized;
    private ILogger<App> _logger = null!;

    public App(IHost host, SingleInstance instance, bool startMinimized)
    {
        _host = host;
        _instance = instance;
        _startMinimized = startMinimized;
    }

    /// <summary>True khi đang thoát thật (từ tray), để MainWindow không chặn lệnh đóng.</summary>
    public bool IsExiting { get; private set; }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services => _host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger = Services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        await _host.StartAsync();

        Services.GetRequiredService<TrayIconService>().Initialize(ShowMainWindow, ExitApplication);
        _instance.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(ShowMainWindow);

        if (!_startMinimized)
        {
            ShowMainWindow();
        }
    }

    public void ShowMainWindow()
    {
        var window = Services.GetRequiredService<MainWindow>();
        if (!window.IsVisible)
        {
            window.Show();
        }
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
        ForegroundWindow.BringToFront(new WindowInteropHelper(window).Handle);
    }

    public void ExitApplication()
    {
        IsExiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetRequiredService<TrayIconService>().Dispose();
        _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Lỗi UI không xử lý được");
        MessageBox.Show($"Đã xảy ra lỗi:\n{e.Exception.Message}", "Maple Classic Helper",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
