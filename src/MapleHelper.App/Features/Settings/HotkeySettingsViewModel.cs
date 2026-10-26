using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Features.Overlay;
using MapleHelper.App.Services;
using MapleHelper.Core.Hotkeys;
using MapleHelper.Core.Settings;

namespace MapleHelper.App.Features.Settings;

public sealed partial class HotkeyRowViewModel : ObservableObject
{
    private readonly Action<bool> _onCapturing;

    public HotkeyRowViewModel(HotkeyBinding binding, Action<bool> onCapturing)
    {
        Action = binding.Action;
        _modifiers = binding.Modifiers;
        _virtualKey = binding.VirtualKey;
        _enabled = binding.Enabled;
        _onCapturing = onCapturing;
    }

    public HotkeyAction Action { get; }
    public string Name => HotkeyText.ActionName(Action);

    [ObservableProperty] private HotkeyModifiers _modifiers;
    [ObservableProperty] private int _virtualKey;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isCapturing;
    [ObservableProperty] private string? _error;

    partial void OnIsCapturingChanged(bool value) => _onCapturing(value);

    public HotkeyBinding ToBinding() => new() { Action = Action, Modifiers = Modifiers, VirtualKey = VirtualKey, Enabled = Enabled };
}

public sealed partial class HotkeySettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IHotkeyService _hotkeys;
    private readonly OverlayCoordinator _coordinator;

    public HotkeySettingsViewModel(ISettingsService settings, IHotkeyService hotkeys, OverlayCoordinator coordinator)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _coordinator = coordinator;
        Load(settings.Current.Hotkeys);
    }

    public ObservableCollection<HotkeyRowViewModel> Rows { get; } = [];

    [ObservableProperty] private string? _status;
    [ObservableProperty] private bool _isError;

    [RelayCommand]
    private async Task ApplyAsync()
    {
        _settings.Current.Hotkeys = Rows.Select(r => r.ToBinding()).ToList();
        await _settings.SaveAsync();
        var failed = _coordinator.ApplyHotkeys();

        foreach (var row in Rows)
        {
            row.Error = failed.Contains(row.Action) ? "Tổ hợp này đã bị app khác dùng hoặc trùng với dòng khác" : null;
        }
        IsError = failed.Count > 0;
        Status = failed.Count == 0 ? "Đã áp dụng hotkey." : $"{failed.Count} hotkey không đăng ký được, hãy đổi tổ hợp khác.";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        Load(HotkeyBinding.CreateDefaults());
        Status = "Đã đặt lại mặc định. Bấm Áp dụng để lưu.";
        IsError = false;
    }

    private void Load(IEnumerable<HotkeyBinding> bindings)
    {
        Rows.Clear();
        foreach (var binding in bindings.OrderBy(b => b.Action))
        {
            // Đang gán phím thì ngắt hotkey toàn cục, kẻo tổ hợp cũ kích hoạt overlay
            Rows.Add(new HotkeyRowViewModel(binding, capturing => _hotkeys.IsSuspended = capturing));
        }
    }
}
