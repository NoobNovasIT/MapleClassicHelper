using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MapleHelper.Core.News;

namespace MapleHelper.App.Features.News;

public sealed partial class NewsItemViewModel(NewsArticle article) : ObservableObject
{
    public NewsArticle Article { get; } = article;

    public long Id => Article.Id;
    public string Title => Article.TitleVi ?? Article.Title;
    public string Summary => Article.SummaryVi ?? Article.Summary;

    /// <summary>Tiêu đề tiếng Anh, chỉ hiện khi đã có bản dịch.</summary>
    public string? OriginalTitle => Article.IsTranslated ? Article.Title : null;

    public bool IsTranslated => Article.IsTranslated;
    public string? ImageUrl => Article.ImageUrl;
    public bool Featured => Article.Featured;
    public string Url => Article.Url;

    public string DateText => Article.LiveDate.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public string CategoryLabel => Labels.Category(Article.Category);

    public Brush CategoryBackground => Labels.CategoryBrush(Article.Category, soft: true);
    public Brush CategoryForeground => Labels.CategoryBrush(Article.Category, soft: false);

    [ObservableProperty]
    private bool _isSaved = article.IsSaved;
}

public static class Labels
{
    public static string Category(NewsCategory category) => category switch
    {
        NewsCategory.General => "Tin chung",
        NewsCategory.Update => "Cập nhật",
        NewsCategory.Sale => "Cash Shop",
        NewsCategory.Events => "Sự kiện",
        NewsCategory.Community => "Cộng đồng",
        NewsCategory.Maintenance => "Bảo trì",
        _ => "Khác",
    };

    public static Brush CategoryBrush(NewsCategory category, bool soft)
    {
        var (background, foreground) = category switch
        {
            NewsCategory.Events => ("#E5F2DA", "#2F5E1C"),
            NewsCategory.Maintenance => ("#FFF0C9", "#7A4E00"),
            NewsCategory.Sale => ("#E3F0FB", "#1F4A7A"),
            NewsCategory.Update => ("#EEE6FB", "#4B2E83"),
            NewsCategory.Community => ("#FBE6F0", "#7A2050"),
            _ => ("#FFE5C2", "#8A460B"),
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(soft ? background : foreground));
        brush.Freeze();
        return brush;
    }
}
