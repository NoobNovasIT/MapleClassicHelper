using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using MapleHelper.App.Features.ChatTranslate;
using MapleHelper.App.Features.Results;
using MapleHelper.App.Features.Snipping;
using MapleHelper.App.Services;
using MapleHelper.Core.Ai;
using MapleHelper.Core.Features.Lookup;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.History;
using MapleHelper.Core.Hotkeys;
using MapleHelper.Core.Settings;
using MapleHelper.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App.Features.Overlay;

/// <summary>
/// Điều phối mọi thứ chạy trên game: nút overlay, hotkey, chụp vùng, popup kết quả, ô chat.
/// Toàn bộ chạy trên UI thread.
/// </summary>
public sealed class OverlayCoordinator(
    IServiceProvider services,
    ISettingsService settings,
    IHotkeyService hotkeys,
    SnippingService snipping,
    ITranslationService translation,
    IItemLookupService itemLookup,
    IQuestGuideService questGuide,
    IHistoryRepository history,
    TrayIconService tray,
    ILogger<OverlayCoordinator> logger)
{
    private readonly List<Window> _openPopups = [];
    private OverlayWindow? _overlay;
    private ChatTranslateWindow? _chat;

    public event EventHandler? OverlayVisibilityChanged;

    public bool IsOverlayVisible => _overlay?.IsVisible == true;

    public void Initialize()
    {
        hotkeys.Pressed += (_, action) => _ = ExecuteAsync(action);
        var failed = ApplyHotkeys();
        if (failed.Count > 0)
        {
            tray.ShowNotification("Một số hotkey không đăng ký được",
                string.Join(", ", failed.Select(HotkeyText.ActionName)) + " đã bị app khác dùng. Đổi tổ hợp trong Cài đặt.");
        }
        if (settings.Current.Overlay.ShowOnStartup)
        {
            ShowOverlay();
        }
    }

    /// <summary>Đăng ký lại hotkey theo settings hiện tại. Trả về các action bị trùng.</summary>
    public IReadOnlyList<HotkeyAction> ApplyHotkeys()
    {
        var failed = hotkeys.Apply(settings.Current.Hotkeys);
        _overlay?.UpdateTooltips(HotkeyTexts());
        return failed;
    }

    public void ShowOverlay()
    {
        var overlay = EnsureOverlay();
        var s = settings.Current.Overlay;
        var area = SystemParameters.WorkArea;
        overlay.PlaceMainCenter(new Point(s.CenterX ?? area.Right - 70, s.CenterY ?? area.Top + area.Height / 2));
        overlay.Show();
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideOverlay()
    {
        _overlay?.CloseMenu();
        _overlay?.Hide();
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleOverlay()
    {
        if (IsOverlayVisible) HideOverlay(); else ShowOverlay();
    }

    public async Task ExecuteAsync(HotkeyAction action)
    {
        var previousForeground = ForegroundWindow.Current;
        try
        {
            switch (action)
            {
                case HotkeyAction.ToggleOverlay:
                    ToggleOverlay();
                    break;
                case HotkeyAction.ChatTranslate:
                    ShowChat(previousForeground);
                    break;
                case HotkeyAction.QuickTranslate:
                    await RunCaptureAsync("Kéo chọn vùng chữ cần dịch", TranslateAsync);
                    break;
                case HotkeyAction.ItemLookup:
                    await RunCaptureAsync("Kéo chọn tooltip hoặc tên vật phẩm", LookupItemAsync);
                    break;
                case HotkeyAction.QuestGuide:
                    await RunCaptureAsync("Kéo chọn tên nhiệm vụ", GuideQuestAsync);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Thực hiện {Action} thất bại", action);
            tray.ShowNotification("Maple Classic Helper", ex.Message);
        }
    }

    private OverlayWindow EnsureOverlay()
    {
        if (_overlay is not null)
        {
            return _overlay;
        }
        _overlay = new OverlayWindow();
        _overlay.UpdateTooltips(HotkeyTexts());
        _overlay.ActionRequested += (_, action) => _ = ExecuteAsync(action);
        _overlay.OpenMainWindowRequested += (_, _) => App.Current.ShowMainWindow();
        _overlay.HideRequested += (_, _) => HideOverlay();
        _overlay.ExitRequested += (_, _) => App.Current.ExitApplication();
        _overlay.MainButtonMoved += async (_, center) =>
        {
            settings.Current.Overlay.CenterX = center.X;
            settings.Current.Overlay.CenterY = center.Y;
            await settings.SaveAsync();
        };
        return _overlay;
    }

    private void ShowChat(nint returnFocusTo)
    {
        _chat ??= services.GetRequiredService<ChatTranslateWindow>();
        if (_chat.IsVisible)
        {
            _chat.Activate();
            return;
        }
        if (_overlay is { IsVisible: true } overlay)
        {
            PopupPlacement.PlaceBeside(_chat, new Rect(overlay.Left, overlay.Top, overlay.Width, overlay.Height));
        }
        else
        {
            PopupPlacement.PlaceNearGameChat(_chat);
        }
        _chat.ShowFor(returnFocusTo);
    }

    /// <summary>Ẩn mọi cửa sổ của app trên màn hình, cho chọn vùng, rồi hiện lại và mở popup kết quả.</summary>
    private async Task RunCaptureAsync(string hint, Func<ResultPopupViewModel, byte[], Task> process)
    {
        var hidden = HideOwnWindows();
        await Task.Delay(150); // chờ DWM vẽ lại màn hình không còn overlay

        SnipResult? snip;
        try
        {
            snip = await snipping.SelectRegionAsync(hint);
        }
        finally
        {
            foreach (var window in hidden)
            {
                window.Show();
            }
        }
        if (snip is null)
        {
            return;
        }

        var png = ScreenCapture.ToPng(snip.Image);
        var viewModel = services.GetRequiredService<ResultPopupViewModel>();
        var popup = new ResultPopupWindow(viewModel);
        popup.Closed += (_, _) => _openPopups.Remove(popup);
        _openPopups.Add(popup);
        PopupPlacement.PlaceBeside(popup, snip.ScreenDip);
        popup.Show();

        try
        {
            await process(viewModel, png);
        }
        catch (AiException ex)
        {
            viewModel.SetError(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Xử lý vùng chụp thất bại");
            viewModel.SetError("Lỗi không mong đợi: " + ex.Message);
        }
        // Kéo lại vào trong màn hình nếu popup cao lên sau khi có nội dung
        PopupPlacement.PlaceBeside(popup, snip.ScreenDip);
    }

    private List<Window> HideOwnWindows()
    {
        var windows = new List<Window>();
        if (_overlay is { IsVisible: true }) windows.Add(_overlay);
        if (_chat is { IsVisible: true }) windows.Add(_chat);
        windows.AddRange(_openPopups.Where(p => p.IsVisible));
        if (services.GetRequiredService<Shell.MainWindow>() is { IsVisible: true, WindowState: not WindowState.Minimized } main) windows.Add(main);
        foreach (var window in windows)
        {
            window.Hide();
        }
        return windows;
    }

    private async Task TranslateAsync(ResultPopupViewModel vm, byte[] png)
    {
        vm.Title = "Dịch vùng chọn";
        vm.Sprite = "mobs/orange-mushroom";
        vm.SetBusy("Đang đọc chữ và dịch…");

        var result = await translation.TranslateScreenAsync(png);
        vm.IsBusy = false;
        vm.Body = result.Translation;
        vm.SourceText = string.IsNullOrWhiteSpace(result.SourceText) ? null : result.SourceText;
        vm.HistoryId = await history.AddAsync(new HistoryEntry
        {
            Kind = HistoryKind.ScreenTranslate,
            Title = FirstLine(result.SourceText),
            Input = result.SourceText,
            Result = result.Translation,
            ImagePng = png,
        });
    }

    private async Task LookupItemAsync(ResultPopupViewModel vm, byte[] png)
    {
        vm.Title = "Tìm nguồn vật phẩm";
        vm.Sprite = "mobs/pig";
        vm.SetBusy("Đang đọc tên vật phẩm…");

        var name = await itemLookup.ReadItemNameAsync(png);
        vm.CanRequery = true;
        vm.Query = name;
        vm.RequeryHandler = query => RunItemLookupAsync(vm, query, png);
        if (string.IsNullOrWhiteSpace(name))
        {
            vm.SetError("Không đọc được tên vật phẩm. Gõ tên tiếng Anh vào ô trên rồi bấm Tra lại.");
            return;
        }
        await RunItemLookupAsync(vm, name, png);
    }

    private async Task RunItemLookupAsync(ResultPopupViewModel vm, string name, byte[] png)
    {
        vm.SetBusy($"Đang tra nguồn gốc \"{name}\"…");
        try
        {
            var result = await itemLookup.LookupAsync(name);
            vm.IsBusy = false;
            vm.Title = result.ItemName;
            vm.Icon = ToImage(result.Item?.IconPng);
            vm.Body = result.Guide;
            vm.SetSources(result.Sources);
            vm.IsSaved = false;
            vm.HistoryId = await history.AddAsync(new HistoryEntry
            {
                Kind = HistoryKind.ItemLookup,
                Title = result.ItemName,
                Input = name,
                Result = result.Guide,
                Sources = result.Sources,
                ImagePng = png,
            });
        }
        catch (AiException ex)
        {
            vm.SetError(ex.Message);
        }
    }

    private async Task GuideQuestAsync(ResultPopupViewModel vm, byte[] png)
    {
        vm.Title = "Hỗ trợ nhiệm vụ";
        vm.Sprite = "mobs/stump";
        vm.SetBusy("Đang đọc tên nhiệm vụ…");

        var name = await questGuide.ReadQuestNameAsync(png);
        vm.CanRequery = true;
        vm.Query = name;
        vm.RequeryHandler = query => RunQuestGuideAsync(vm, query, png);
        if (string.IsNullOrWhiteSpace(name))
        {
            vm.SetError("Không đọc được tên nhiệm vụ. Gõ tên tiếng Anh vào ô trên rồi bấm Tra lại.");
            return;
        }
        await RunQuestGuideAsync(vm, name, png);
    }

    private async Task RunQuestGuideAsync(ResultPopupViewModel vm, string name, byte[] png)
    {
        vm.SetBusy($"Đang tìm hướng dẫn \"{name}\"…");
        try
        {
            var result = await questGuide.GuideAsync(name);
            vm.IsBusy = false;
            vm.Title = result.QuestName;
            vm.Body = result.Guide;
            vm.SetSources(result.Sources);
            vm.ExternalLinkLabel = "Tìm video hướng dẫn trên YouTube";
            vm.ExternalLinkUrl = result.YouTubeSearchUrl;
            vm.IsSaved = false;
            vm.HistoryId = await history.AddAsync(new HistoryEntry
            {
                Kind = HistoryKind.QuestGuide,
                Title = result.QuestName,
                Input = name,
                Result = result.Guide,
                Sources = result.Sources,
                ImagePng = png,
            });
        }
        catch (AiException ex)
        {
            vm.SetError(ex.Message);
        }
    }

    private IReadOnlyDictionary<HotkeyAction, string> HotkeyTexts() =>
        settings.Current.Hotkeys.Where(h => h.Enabled).ToDictionary(h => h.Action, HotkeyText.Format);

    private static string? FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return line is null ? null : line.Length > 60 ? line[..60] + "…" : line;
    }

    internal static BitmapSource? ToImage(byte[]? png)
    {
        if (png is null || png.Length == 0)
        {
            return null;
        }
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(png);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
