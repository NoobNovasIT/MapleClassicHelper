using System.Reflection;
using MapleHelper.Core.Settings;
using MapleHelper.Core.Updates;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace MapleHelper.App.Services;

/// <summary>
/// Cập nhật qua Velopack, đọc bản phát hành từ GitHub Releases của repo trong Cài đặt.
/// Chỉ hoạt động với bản cài bằng Setup.exe (build/publish.ps1), không chạy khi debug từ Visual Studio.
/// </summary>
public sealed class VelopackUpdateService(ISettingsService settings, ILogger<VelopackUpdateService> logger) : IUpdateService
{
    private UpdateManager? _manager;
    private UpdateInfo? _pending;
    private string? _managerRepo;

    public string CurrentVersion =>
        TryCreateManager()?.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Current.Updates.RepositoryUrl))
        {
            return new UpdateCheckResult(UpdateStatus.NotConfigured, Message: "Chưa cấu hình repo GitHub để nhận bản cập nhật.");
        }

        var manager = TryCreateManager();
        if (manager is null || !manager.IsInstalled)
        {
            return new UpdateCheckResult(UpdateStatus.NotInstalled, Message: "Đang chạy bản dev, chỉ bản cài bằng Setup.exe mới tự cập nhật.");
        }

        try
        {
            _pending = await manager.CheckForUpdatesAsync();
            return _pending is null
                ? new UpdateCheckResult(UpdateStatus.UpToDate, Message: $"Đang dùng bản mới nhất ({CurrentVersion}).")
                : new UpdateCheckResult(UpdateStatus.Available, _pending.TargetFullRelease.Version.ToString(),
                    $"Có bản mới {_pending.TargetFullRelease.Version}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kiểm tra cập nhật thất bại");
            return new UpdateCheckResult(UpdateStatus.Error, Message: "Không kiểm tra được bản cập nhật: " + ex.Message);
        }
    }

    public async Task DownloadAndRestartAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var manager = TryCreateManager();
        if (manager is null || _pending is null)
        {
            return;
        }
        await manager.DownloadUpdatesAsync(_pending, p => progress?.Report(p), cancelToken: ct);
        manager.ApplyUpdatesAndRestart(_pending);
    }

    private UpdateManager? TryCreateManager()
    {
        var repo = settings.Current.Updates.RepositoryUrl.Trim();
        if (_manager is not null && _managerRepo == repo)
        {
            return _manager;
        }
        _manager = null;
        _pending = null;
        if (string.IsNullOrWhiteSpace(repo))
        {
            return null;
        }
        try
        {
            _manager = new UpdateManager(new GithubSource(repo, accessToken: null, prerelease: false));
            _managerRepo = repo;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không tạo được UpdateManager cho {Repo}", repo);
        }
        return _manager;
    }
}
