using System.Windows.Input;
using MapleHelper.Core.Settings;

namespace MapleHelper.App.Services;

public static class HotkeyText
{
    public static string Format(HotkeyModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        if (virtualKey != 0)
        {
            parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(virtualKey)));
        }
        return string.Join(" + ", parts);
    }

    public static string Format(HotkeyBinding binding) => Format(binding.Modifiers, binding.VirtualKey);

    public static string ActionName(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleOverlay => "Bật / tắt overlay",
        HotkeyAction.QuickTranslate => "1 · Dịch vùng chọn",
        HotkeyAction.ItemLookup => "2 · Tìm nguồn item",
        HotkeyAction.QuestGuide => "3 · Hỗ trợ quest",
        HotkeyAction.ChatTranslate => "4 · Chat Việt → Anh",
        _ => action.ToString(),
    };

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (int)(key - Key.NumPad0),
        Key.Oem3 => "`",
        Key.OemMinus => "-",
        Key.OemPlus => "=",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.Space => "Space",
        _ => key.ToString(),
    };
}
