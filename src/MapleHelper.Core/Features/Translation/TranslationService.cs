using MapleHelper.Core.Ai;

namespace MapleHelper.Core.Features.Translation;

public sealed record ScreenTranslation(string SourceText, string Translation);

public interface ITranslationService
{
    /// <summary>Câu chat tiếng Việt → tiếng Anh kiểu game thủ, để dán vào ô chat.</summary>
    Task<string> TranslateChatAsync(string vietnamese, CancellationToken ct = default);

    /// <summary>Đọc chữ trong ảnh chụp giao diện game và dịch sang tiếng Việt.</summary>
    Task<ScreenTranslation> TranslateScreenAsync(byte[] png, CancellationToken ct = default);
}

public sealed class TranslationService(IAiService ai, IMachineTranslator machine) : ITranslationService
{
    internal const string ScreenPrompt = """
        Ảnh là một phần giao diện game MapleStory Classic World (hội thoại NPC, nhiệm vụ, thông báo, mô tả vật phẩm, kỹ năng...).
        1. Đọc chính xác toàn bộ chữ tiếng Anh trong ảnh, giữ xuống dòng hợp lý.
        2. Dịch sang tiếng Việt tự nhiên, dễ hiểu cho người chơi.

        Quy tắc:
        - Giữ nguyên tên riêng tiếng Anh của quái, NPC, bản đồ, vật phẩm, kỹ năng vì người chơi cần tên gốc để tra cứu.
          Lần đầu xuất hiện có thể thêm nghĩa tiếng Việt trong ngoặc, ví dụ: Orange Mushroom Cap (Mũ Nấm Cam).
        - Giữ nguyên số lượng, chỉ số, cấp độ.
        - Nếu ảnh không có chữ, để "source" rỗng và "translation" là "Không tìm thấy chữ trong ảnh."

        Chỉ trả về đúng một JSON: {"source": "<chữ gốc>", "translation": "<bản dịch tiếng Việt>"}
        """;

    /// <summary>Chat cần nhanh nên dùng Google Dịch, không qua AI.</summary>
    public async Task<string> TranslateChatAsync(string vietnamese, CancellationToken ct = default) =>
        CleanChatOutput(await machine.TranslateAsync(vietnamese.Trim(), "vi", "en", ct));

    public async Task<ScreenTranslation> TranslateScreenAsync(byte[] png, CancellationToken ct = default)
    {
        var response = await ai.GenerateAsync(new AiRequest
        {
            Prompt = ScreenPrompt,
            ImagePng = png,
            MaxOutputTokens = 4096,
        }, ct);
        return ParseScreenTranslation(response.Text);
    }

    /// <summary>Bỏ ngoặc kép bao quanh mà model đôi khi tự thêm.</summary>
    internal static string CleanChatOutput(string text)
    {
        var result = text.Trim();
        if (result.Length >= 2 && result[0] == '"' && result[^1] == '"')
        {
            result = result[1..^1].Trim();
        }
        return result;
    }

    internal static ScreenTranslation ParseScreenTranslation(string text)
    {
        var json = AiJson.TryParse(text);
        var translation = json.GetString("translation");
        if (translation is null)
        {
            // Model không trả JSON: hiển thị nguyên văn còn hơn báo lỗi
            return new ScreenTranslation("", text.Trim());
        }
        return new ScreenTranslation(json.GetString("source")?.Trim() ?? "", translation.Trim());
    }
}
