using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Features.History;
using MapleHelper.App.Features.News;
using MapleHelper.App.Services;
using MapleHelper.Core.History;
using MapleHelper.Core.News;
using MapleHelper.Core.Settings;

namespace MapleHelper.App.Features.Saved;

/// <summary>Gom các tin đã lưu và các kết quả tra cứu đã gắn sao.</summary>
public sealed partial class SavedViewModel(
    INewsRepository news,
    IHistoryRepository history,
    ISettingsService settings) : ObservableObject
{
    public ObservableCollection<NewsItemViewModel> Articles { get; } = [];
    public ObservableCollection<HistoryItemViewModel> Entries { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>MainViewModel nghe để chuyển sang trang Điểm tin và mở bài.</summary>
    public event EventHandler<NewsItemViewModel>? OpenNewsRequested;

    public async Task ReloadAsync()
    {
        var articles = await news.ListAsync(new NewsQuery(ClassicOnly: false, SavedOnly: true));
        Articles.Clear();
        foreach (var article in articles)
        {
            Articles.Add(new NewsItemViewModel(article));
        }

        var entries = await history.ListAsync(savedOnly: true);
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(new HistoryItemViewModel(entry));
        }
        IsEmpty = Articles.Count == 0 && Entries.Count == 0;
    }

    // Các lệnh dưới đây trùng tên với NewsViewModel/HistoryViewModel để dùng chung DataTemplate

    [RelayCommand]
    private void Open(NewsItemViewModel item) => OpenNewsRequested?.Invoke(this, item);

    [RelayCommand]
    private static void OpenOriginal(NewsItemViewModel item) =>
        Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });

    [RelayCommand]
    private async Task ToggleSaveAsync(object item)
    {
        switch (item)
        {
            case NewsItemViewModel article:
                await news.SetSavedAsync(article.Id, false);
                Articles.Remove(article);
                break;
            case HistoryItemViewModel entry:
                await history.SetSavedAsync(entry.Id, false);
                Entries.Remove(entry);
                break;
        }
        IsEmpty = Articles.Count == 0 && Entries.Count == 0;
    }

    [RelayCommand]
    private static void ToggleExpand(HistoryItemViewModel item) => item.IsExpanded = !item.IsExpanded;

    [RelayCommand]
    private static void Copy(HistoryItemViewModel item) => ClipboardService.TrySetText(item.Result);

    [RelayCommand]
    private async Task DeleteAsync(HistoryItemViewModel item)
    {
        Entries.Remove(item);
        await history.DeleteAsync(item.Id);
        IsEmpty = Articles.Count == 0 && Entries.Count == 0;
    }

    public bool ClassicOnly => settings.Current.News.ClassicOnly;
}
