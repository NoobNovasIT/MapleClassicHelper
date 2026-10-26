namespace MapleHelper.Core.Updates;

public enum UpdateStatus
{
    /// <summary>Chạy từ thư mục build (Visual Studio), không phải bản cài bằng Setup.exe.</summary>
    NotInstalled,
    NotConfigured,
    UpToDate,
    Available,
    Error,
}

public sealed record UpdateCheckResult(UpdateStatus Status, string? NewVersion = null, string? Message = null);

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);

    /// <summary>Tải bản mới (đã kiểm tra bằng <see cref="CheckAsync"/>) rồi khởi động lại app.</summary>
    Task DownloadAndRestartAsync(IProgress<int>? progress = null, CancellationToken ct = default);
}
