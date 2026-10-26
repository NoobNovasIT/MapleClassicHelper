using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.News;
using MapleHelper.Core.Settings;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App.Features.News;

public sealed record CategoryFilter(string Label, NewsCategory? Category);

public sealed partial class NewsViewModel : ObservableObject
{
    private readonly INewsService _news;
    private readonly INewsRepository _repository;
    private readonly ISettingsService _settings;
    private readonly ILogger<NewsViewModel> _logger;
    private bool _reloadQueued;

    public NewsViewModel(INewsService news, INewsRepository repository, ISettingsService settings, ILogger<NewsViewModel> logger)
    {
        _news = news;
        _repository = repository;
        _settings = settings;
        _logger = logger;
        _selectedCategory = Categories[0];

        // Dịch nền xong từng đợt thì cập nhật danh sách (gộp nhiều lần báo thành một lần tải lại)
        news.Updated += (_, _) => Application.Current.Dispatcher.BeginInvoke(QueueReload);
    }

    public IReadOnlyList<CategoryFilter> Categories { get; } =
    [
        new("Tất cả", null),
        new("Tin chung", NewsCategory.General),
        new("Cập nhật", NewsCategory.Update),
        new("Sự kiện", NewsCategory.Events),
        new("Cash Shop", NewsCategory.Sale),
        new("Bảo trì", NewsCategory.Maintenance),
        new("Cộng đồng", NewsCategory.Community),
    ];

    public ObservableCollection<NewsItemViewModel> Articles { get; } = [];

    [ObservableProperty]
    private CategoryFilter _selectedCategory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private ArticleDetailViewModel? _detail;

    public bool IsLoaded { get; private set; }

    partial void OnSelectedCategoryChanged(CategoryFilter value) => _ = ReloadAsync();

    /// <summary>Gọi khi trang được mở lần đầu.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }
        IsLoaded = true;
        await ReloadAsync();
        if (Articles.Count == 0)
        {
            await RefreshAsync();
        }
    }

    public async Task ReloadAsync()
    {
        var query = new NewsQuery(SelectedCategory.Category, _settings.Current.News.ClassicOnly);
        var articles = await _repository.ListAsync(query);
        Articles.Clear();
        foreach (var article in articles)
        {
            Articles.Add(new NewsItemViewModel(article));
        }

        var untranslated = articles.Count(a => !a.IsTranslated);
        if (!IsRefreshing)
        {
            Status = articles.Count == 0 ? null
                : untranslated > 0 ? $"{articles.Count} bài · còn {untranslated} bài đang chờ dịch"
                : $"{articles.Count} bài";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        Status = "Đang tải tin từ Nexon…";
        try
        {
            var added = await _news.RefreshAsync();
            await ReloadAsync();
            Status = added > 0 ? $"Có {added} bài mới. Đang dịch…" : "Đang dịch các bài chưa dịch…";
            await _news.TranslatePendingAsync();
            Status = null;
        }
        catch (MachineTranslationException ex)
        {
            Status = "Dịch tin thất bại: " + ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Làm mới tin thất bại");
            Status = "Không tải được tin từ Nexon. Kiểm tra mạng rồi thử lại.";
        }
        finally
        {
            IsRefreshing = false;
            await ReloadAsync();
        }
    }

    private bool CanRefresh() => !IsRefreshing;

    [RelayCommand]
    private async Task OpenAsync(NewsItemViewModel item)
    {
        Detail = new ArticleDetailViewModel(item, _news, _repository, () => Detail = null);
        await Detail.LoadAsync();
    }

    [RelayCommand]
    private static void OpenOriginal(NewsItemViewModel item) =>
        Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });

    [RelayCommand]
    private async Task ToggleSaveAsync(NewsItemViewModel item)
    {
        item.IsSaved = !item.IsSaved;
        item.Article.IsSaved = item.IsSaved;
        await _repository.SetSavedAsync(item.Id, item.IsSaved);
    }

    private async void QueueReload()
    {
        if (_reloadQueued || !IsLoaded)
        {
            return;
        }
        _reloadQueued = true;
        await Task.Delay(400);
        _reloadQueued = false;
        await ReloadAsync();
    }
}

public sealed partial class ArticleDetailViewModel(
    NewsItemViewModel item,
    INewsService news,
    INewsRepository repository,
    Action close) : ObservableObject
{
    public NewsItemViewModel Item { get; } = item;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _body;

    [ObservableProperty]
    private string? _originalBody;

    [ObservableProperty]
    private bool _showOriginal;

    [ObservableProperty]
    private string? _error;

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var article = await news.GetDetailAsync(Item.Id);
            Body = article?.BodyVi;
            OriginalBody = article?.BodyHtml is { } html ? HtmlText.ToMarkdown(html) : null;
        }
        catch (MachineTranslationException ex)
        {
            Error = ex.Message;
            await LoadOriginalOnlyAsync();
        }
        catch (Exception ex)
        {
            Error = "Không tải được bài viết: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Dịch thất bại (chưa có key...) thì vẫn cho đọc bản tiếng Anh.</summary>
    private async Task LoadOriginalOnlyAsync()
    {
        var article = await repository.GetAsync(Item.Id);
        if (article?.BodyHtml is { } html)
        {
            OriginalBody = HtmlText.ToMarkdown(html);
            ShowOriginal = true;
        }
    }

    [RelayCommand]
    private void Back() => close();

    [RelayCommand]
    private void ToggleOriginal() => ShowOriginal = !ShowOriginal;

    [RelayCommand]
    private void OpenOriginal() => Process.Start(new ProcessStartInfo(Item.Url) { UseShellExecute = true });
}
