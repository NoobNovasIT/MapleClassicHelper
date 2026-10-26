using MapleHelper.Core.Ai;

namespace MapleHelper.Core.Settings;

public sealed class AppSettings
{
    public AiSettings Ai { get; set; } = new();
    public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.CreateDefaults();
    public NewsSettings News { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public MapleDataSettings MapleData { get; set; } = new();
    public UpdateSettings Updates { get; set; } = new();

    /// <summary>Bấm X trên cửa sổ chính thì ẩn xuống khay hệ thống thay vì thoát.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;
}

public sealed class AiSettings
{
    public AiProviderKind ActiveProvider { get; set; } = AiProviderKind.Gemini;

    /// <summary>Model người dùng chọn cho từng provider. Thiếu thì dùng DefaultModel của provider.</summary>
    public Dictionary<AiProviderKind, string> Models { get; set; } = [];

    public string? GetModel(AiProviderKind kind) => Models.GetValueOrDefault(kind);
}

public sealed class NewsSettings
{
    /// <summary>
    /// JSON mà trang nexon.com/maplestory/news dùng. Bài viết: {FeedUrl}/{id}. Ảnh: https://g.nexonstatic.com + imageThumbnail.
    /// </summary>
    public string FeedUrl { get; set; } = "https://g.nexonstatic.com/maplestory/cms/v1/news";
    public int PollIntervalMinutes { get; set; } = 30;

    /// <summary>Chỉ lấy tin của MapleStory Classic World (bỏ tin của bản MapleStory hiện đại).</summary>
    public bool ClassicOnly { get; set; } = true;

    /// <summary>Hiện thông báo ở khay hệ thống khi có tin mới.</summary>
    public bool NotifyNewArticles { get; set; } = true;
}

/// <summary>Nguồn dữ liệu game maplestory.io. GMS v83 là bản gần với Classic World nhất hiện có.</summary>
public sealed class MapleDataSettings
{
    public string ApiBaseUrl { get; set; } = "https://maplestory.io/api";
    public string Region { get; set; } = "GMS";
    public string Version { get; set; } = "83";
}

public sealed class UpdateSettings
{
    /// <summary>Repo GitHub chứa Releases do Velopack tạo, ví dụ https://github.com/owner/MapleClassicHelper.</summary>
    public string RepositoryUrl { get; set; } = "";
    public bool CheckOnStartup { get; set; } = true;
}

public sealed class OverlaySettings
{
    /// <summary>Tâm nút tròn chính trên màn hình (DIP). Null = góc phải giữa màn hình chính.</summary>
    public double? CenterX { get; set; }
    public double? CenterY { get; set; }

    /// <summary>Sau khi copy câu chat thì tự đóng ô nhập và trả focus về game.</summary>
    public bool AutoCloseChatAfterCopy { get; set; } = true;

    /// <summary>Mở overlay luôn khi khởi động app.</summary>
    public bool ShowOnStartup { get; set; }
}

public enum HotkeyAction
{
    ToggleOverlay,
    QuickTranslate,
    ItemLookup,
    QuestGuide,
    ChatTranslate,
}

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Tổ hợp phím toàn cục. VirtualKey là mã VK_* của Win32 để Core không phụ thuộc WPF.</summary>
public sealed class HotkeyBinding
{
    public HotkeyAction Action { get; set; }
    public HotkeyModifiers Modifiers { get; set; }
    public int VirtualKey { get; set; }
    public bool Enabled { get; set; } = true;

    private const HotkeyModifiers CtrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;

    public static List<HotkeyBinding> CreateDefaults() =>
    [
        new() { Action = HotkeyAction.ToggleOverlay, Modifiers = CtrlShift, VirtualKey = 0x4D },  // M
        new() { Action = HotkeyAction.QuickTranslate, Modifiers = CtrlShift, VirtualKey = 0x31 }, // 1
        new() { Action = HotkeyAction.ItemLookup, Modifiers = CtrlShift, VirtualKey = 0x32 },     // 2
        new() { Action = HotkeyAction.QuestGuide, Modifiers = CtrlShift, VirtualKey = 0x33 },     // 3
        new() { Action = HotkeyAction.ChatTranslate, Modifiers = CtrlShift, VirtualKey = 0x34 },  // 4
    ];
}
