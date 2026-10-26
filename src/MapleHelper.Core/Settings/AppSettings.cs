using MapleHelper.Core.Ai;

namespace MapleHelper.Core.Settings;

public sealed class AppSettings
{
    public AiSettings Ai { get; set; } = new();
    public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.CreateDefaults();
    public NewsSettings News { get; set; } = new();

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
    public string SourceUrl { get; set; } = "https://www.nexon.com/maplestory/news/all?page=1&gameVersion=mscw";
    public int PollIntervalMinutes { get; set; } = 30;
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
