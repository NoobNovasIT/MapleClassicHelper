using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Services;
using MapleHelper.Core.Ai;
using MapleHelper.Core.History;

namespace MapleHelper.App.Features.Results;

public sealed record ExternalLink(string Label, string Url);

/// <summary>Popup kết quả dùng chung cho nút 1 (dịch), 2 (item), 3 (quest).</summary>
public sealed partial class ResultPopupViewModel(IHistoryRepository history) : ObservableObject
{
    public ObservableCollection<AiSource> Sources { get; } = [];

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _sprite = "mobs/orange-mushroom";
    [ObservableProperty] private ImageSource? _icon;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _busyText;
    [ObservableProperty] private string? _error;

    /// <summary>Nội dung chính (markdown).</summary>
    [ObservableProperty] private string? _body;

    /// <summary>Chữ gốc đọc được từ ảnh (chỉ với nút dịch).</summary>
    [ObservableProperty] private string? _sourceText;
    [ObservableProperty] private bool _isSourceVisible;

    /// <summary>Tên item/quest đọc được, cho phép sửa rồi tra lại.</summary>
    [ObservableProperty] private string? _query;
    [ObservableProperty] private bool _canRequery;

    /// <summary>Link mở ngoài: "Xem trên NiaMeowDB", video YouTube...</summary>
    public ObservableCollection<ExternalLink> ExternalLinks { get; } = [];

    public void SetExternalLinks(params ExternalLink[] links)
    {
        ExternalLinks.Clear();
        foreach (var link in links)
        {
            ExternalLinks.Add(link);
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleSaveCommand))]
    private long? _historyId;

    [ObservableProperty] private bool _isSaved;

    /// <summary>Gán bởi coordinator: chạy lại tra cứu với tên mới.</summary>
    public Func<string, Task>? RequeryHandler { get; set; }

    public void SetBusy(string text)
    {
        Error = null;
        BusyText = text;
        IsBusy = true;
    }

    public void SetError(string message)
    {
        IsBusy = false;
        Error = message;
    }

    public void SetSources(IEnumerable<AiSource> sources)
    {
        Sources.Clear();
        foreach (var source in sources.Take(8))
        {
            Sources.Add(source);
        }
    }

    [RelayCommand]
    private void Copy()
    {
        var text = string.IsNullOrWhiteSpace(Body) ? SourceText : Body;
        if (!string.IsNullOrWhiteSpace(text))
        {
            ClipboardService.TrySetText(text);
        }
    }

    [RelayCommand]
    private void ToggleSource() => IsSourceVisible = !IsSourceVisible;

    [RelayCommand(CanExecute = nameof(CanToggleSave))]
    private async Task ToggleSaveAsync()
    {
        IsSaved = !IsSaved;
        await history.SetSavedAsync(HistoryId!.Value, IsSaved);
    }

    private bool CanToggleSave() => HistoryId is not null;

    [RelayCommand]
    private async Task RequeryAsync()
    {
        if (RequeryHandler is not null && !string.IsNullOrWhiteSpace(Query) && !IsBusy)
        {
            await RequeryHandler(Query.Trim());
        }
    }

    [RelayCommand]
    private static void OpenUrl(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
