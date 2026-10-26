using System.Windows.Interop;
using MapleHelper.Core.Hotkeys;
using MapleHelper.Core.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MapleHelper.Native;

/// <summary>
/// Hotkey toàn cục qua RegisterHotKey, nhận WM_HOTKEY bằng một message-only window.
/// Phải tạo và dùng trên UI thread (dispatcher WPF).
/// </summary>
public sealed class GlobalHotkeyService : IHotkeyService, IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, HotkeyAction> _registered = [];
    private int _nextId = 1;

    public GlobalHotkeyService()
    {
        // HWND_MESSAGE (-3): cửa sổ ẩn chỉ để nhận message
        var parameters = new HwndSourceParameters("MapleHelper.Hotkeys") { ParentWindow = new IntPtr(-3) };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public event EventHandler<HotkeyAction>? Pressed;

    public bool IsSuspended { get; set; }

    public IReadOnlyList<HotkeyAction> Apply(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();

        var failed = new List<HotkeyAction>();
        var seen = new HashSet<(HotkeyModifiers, int)>();
        foreach (var binding in bindings.Where(b => b.Enabled && b.VirtualKey != 0))
        {
            if (!seen.Add((binding.Modifiers, binding.VirtualKey)))
            {
                failed.Add(binding.Action); // trùng với action khác trong app
                continue;
            }

            var id = _nextId++;
            var modifiers = ToNative(binding.Modifiers) | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            if (PInvoke.RegisterHotKey(Hwnd, id, modifiers, (uint)binding.VirtualKey))
            {
                _registered[id] = binding.Action;
            }
            else
            {
                failed.Add(binding.Action);
            }
        }
        return failed;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private HWND Hwnd => (HWND)_source.Handle;

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys)
        {
            PInvoke.UnregisterHotKey(Hwnd, id);
        }
        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)PInvoke.WM_HOTKEY && _registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            if (!IsSuspended)
            {
                Pressed?.Invoke(this, action);
            }
        }
        return IntPtr.Zero;
    }

    private static HOT_KEY_MODIFIERS ToNative(HotkeyModifiers modifiers)
    {
        HOT_KEY_MODIFIERS result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= HOT_KEY_MODIFIERS.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) result |= HOT_KEY_MODIFIERS.MOD_WIN;
        return result;
    }
}
