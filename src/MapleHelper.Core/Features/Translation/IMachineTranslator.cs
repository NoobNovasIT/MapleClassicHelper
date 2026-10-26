namespace MapleHelper.Core.Features.Translation;

/// <summary>Dịch máy nhanh, không cần AI (dùng cho chat ngắn).</summary>
public interface IMachineTranslator
{
    /// <param name="from">Mã ngôn ngữ nguồn, ví dụ "vi". "auto" để tự nhận.</param>
    /// <param name="to">Mã ngôn ngữ đích, ví dụ "en".</param>
    Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default);
}

public sealed class MachineTranslationException(string message, Exception? inner = null) : Exception(message, inner);
