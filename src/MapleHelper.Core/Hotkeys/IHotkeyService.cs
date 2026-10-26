using MapleHelper.Core.Settings;

namespace MapleHelper.Core.Hotkeys;

public interface IHotkeyService
{
    /// <summary>Phát trên UI thread khi người dùng bấm một tổ hợp đã đăng ký.</summary>
    event EventHandler<HotkeyAction>? Pressed;

    /// <summary>
    /// Huỷ toàn bộ đăng ký cũ rồi đăng ký lại. Trả về các action không đăng ký được
    /// (tổ hợp đã bị app khác giữ, hoặc trùng nhau).
    /// </summary>
    IReadOnlyList<HotkeyAction> Apply(IEnumerable<HotkeyBinding> bindings);

    /// <summary>Tạm ngắt khi người dùng đang gán phím mới, để tổ hợp cũ không kích hoạt.</summary>
    bool IsSuspended { get; set; }
}
