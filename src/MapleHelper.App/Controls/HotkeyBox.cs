using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MapleHelper.App.Services;
using MapleHelper.Core.Settings;

namespace MapleHelper.App.Controls;

/// <summary>
/// Ô bắt tổ hợp phím: bấm vào rồi nhấn tổ hợp mong muốn. Backspace/Delete để xoá.
/// Bắt buộc có Ctrl/Alt/Shift/Win, trừ phím F1-F24.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty ModifiersProperty = DependencyProperty.Register(
        nameof(Modifiers), typeof(HotkeyModifiers), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(HotkeyModifiers.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public static readonly DependencyProperty VirtualKeyProperty = DependencyProperty.Register(
        nameof(VirtualKey), typeof(int), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public static readonly DependencyProperty IsCapturingProperty = DependencyProperty.Register(
        nameof(IsCapturing), typeof(bool), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        UpdateText();
    }

    public HotkeyModifiers Modifiers
    {
        get => (HotkeyModifiers)GetValue(ModifiersProperty);
        set => SetValue(ModifiersProperty, value);
    }

    public int VirtualKey
    {
        get => (int)GetValue(VirtualKeyProperty);
        set => SetValue(VirtualKeyProperty, value);
    }

    /// <summary>True khi ô đang có focus, để tạm ngắt hotkey toàn cục.</summary>
    public bool IsCapturing
    {
        get => (bool)GetValue(IsCapturingProperty);
        set => SetValue(IsCapturingProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        IsCapturing = true;
        Text = "Nhấn tổ hợp phím…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        IsCapturing = false;
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            Modifiers = HotkeyModifiers.None;
            VirtualKey = 0;
            UpdateText();
            return;
        }
        if (key == Key.Tab)
        {
            e.Handled = false; // vẫn cho chuyển ô bằng Tab
            return;
        }

        var modifiers = CurrentModifiers();
        if (IsModifierKey(key))
        {
            Text = HotkeyText.Format(modifiers, 0) + " + …";
            return;
        }

        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (modifiers == HotkeyModifiers.None && !isFunctionKey)
        {
            Text = "Cần kèm Ctrl, Alt hoặc Shift";
            return;
        }

        Modifiers = modifiers;
        VirtualKey = KeyInterop.VirtualKeyFromKey(key);
        UpdateText();
        // Gán xong thì rời ô để hotkey hoạt động lại
        MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private static HotkeyModifiers CurrentModifiers()
    {
        var result = HotkeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Win;
        return result;
    }

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (!box.IsKeyboardFocused)
        {
            box.UpdateText();
        }
    }

    private void UpdateText() =>
        Text = VirtualKey == 0 ? "(chưa gán)" : HotkeyText.Format(Modifiers, VirtualKey);
}
