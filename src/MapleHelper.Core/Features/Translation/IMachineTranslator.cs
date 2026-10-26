namespace MapleHelper.Core.Features.Translation;

/// <summary>Dịch máy nhanh, không cần AI (dùng cho chat ngắn).</summary>
public interface IMachineTranslator
{
    /// <param name="from">Mã ngôn ngữ nguồn, ví dụ "vi". "auto" để tự nhận.</param>
    /// <param name="to">Mã ngôn ngữ đích, ví dụ "en".</param>
    /// <param name="background">
    /// Việc chạy nền (dịch tin): chỉ dùng nguồn chính, không đụng tới nguồn dự phòng để dành cho chat và dịch vùng chọn.
    /// </param>
    Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default, bool background = false);
}

public class MachineTranslationException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Dịch vụ dịch tạm chặn vì gọi quá nhiều (HTTP 429). Việc chạy nền nên dừng và thử lại sau.</summary>
public sealed class MachineTranslationRateLimitedException(string message) : MachineTranslationException(message);
