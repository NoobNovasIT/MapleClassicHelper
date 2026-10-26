using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Features.Overlay;
using MapleHelper.App.Services;
using MapleHelper.Core.History;

namespace MapleHelper.App.Features.History;

public sealed record HistoryFilter(string Label, HistoryKind? Kind);

public sealed partial class HistoryItemViewModel(HistoryEntry entry) : ObservableObject
{
    private ImageSource? _image;

    public HistoryEntry Entry { get; } = entry;
    public long Id => Entry.Id;
    public string Title => Entry.DisplayTitle;
    public string Result => Entry.Result;
    public bool HasImage => Entry.ImagePng is { Length: > 0 };

    public string Preview
    {
        get
        {
            var text = Entry.Result.Replace("#", "").Replace("**", "").Replace('\n', ' ').Trim();
            return text.Length > 140 ? text[..140] + "…" : text;
        }
    }

    public string TimeText => Entry.CreatedAt.ToLocalTime().ToString("HH:mm · dd/MM/yyyy", CultureInfo.InvariantCulture);

    public string KindLabel => Entry.Kind switch
    {
        HistoryKind.ScreenTranslate => "Dịch vùng chọn",
        HistoryKind.ItemLookup => "Tìm item",
        HistoryKind.QuestGuide => "Quest",
        HistoryKind.ChatTranslate => "Chat Việt → Anh",
        _ => "",
    };

    public string Sprite => Entry.Kind switch
    {
        HistoryKind.ScreenTranslate => "mobs/orange-mushroom",
        HistoryKind.ItemLookup => "mobs/pig",
        HistoryKind.QuestGuide => "mobs/stump",
        _ => "mobs/blue-snail",
    };

    /// <summary>Ảnh vùng chụp, chỉ giải mã khi mở rộng mục.</summary>
    public ImageSource? Image => _image ??= OverlayCoordinator.ToImage(Entry.ImagePng);

    public IReadOnlyList<Core.Ai.AiSource> Sources => Entry.Sources;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSaved = entry.IsSaved;
}

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryRepository _history;
    private bool _reloadQueued;

    public HistoryViewModel(IHistoryRepository history)
    {
        _history = history;
        _selectedFilter = Filters[0];
        history.Changed += (_, _) => Application.Current.Dispatcher.BeginInvoke(QueueReload);
    }

    public IReadOnlyList<HistoryFilter> Filters { get; } =
    [
        new("Tất cả", null),
        new("Dịch vùng chọn", HistoryKind.ScreenTranslate),
        new("Tìm item", HistoryKind.ItemLookup),
        new("Quest", HistoryKind.QuestGuide),
        new("Chat Việt → Anh", HistoryKind.ChatTranslate),
    ];

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private HistoryFilter _selectedFilter;

    partial void OnSelectedFilterChanged(HistoryFilter value) => _ = ReloadAsync();

    public async Task ReloadAsync()
    {
        var entries = await _history.ListAsync(SelectedFilter.Kind);
        Items.Clear();
        foreach (var entry in entries)
        {
            Items.Add(new HistoryItemViewModel(entry));
        }
    }

    [RelayCommand]
    private static void ToggleExpand(HistoryItemViewModel item) => item.IsExpanded = !item.IsExpanded;

    [RelayCommand]
    private static void Copy(HistoryItemViewModel item) => ClipboardService.TrySetText(item.Result);

    [RelayCommand]
    private async Task ToggleSaveAsync(HistoryItemViewModel item)
    {
        item.IsSaved = !item.IsSaved;
        await _history.SetSavedAsync(item.Id, item.IsSaved);
    }

    [RelayCommand]
    private async Task DeleteAsync(HistoryItemViewModel item)
    {
        Items.Remove(item);
        await _history.DeleteAsync(item.Id);
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var answer = MessageBox.Show("Xoá toàn bộ lịch sử? Các mục đã lưu (ngôi sao) sẽ được giữ lại.", "Maple Classic Helper",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.OK)
        {
            await _history.ClearUnsavedAsync();
        }
    }

    private async void QueueReload()
    {
        if (_reloadQueued)
        {
            return;
        }
        _reloadQueued = true;
        await Task.Delay(300);
        _reloadQueued = false;
        await ReloadAsync();
    }
}
