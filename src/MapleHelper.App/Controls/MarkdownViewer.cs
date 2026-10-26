using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MapleHelper.App.Controls;

/// <summary>
/// Hiển thị markdown đơn giản mà AI trả về (#, -, 1., **đậm**, link, bảng |) trong RichTextBox chỉ đọc,
/// để người dùng bôi đen copy được.
/// </summary>
public sealed partial class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownViewer), new PropertyMetadata(null, OnMarkdownChanged));

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        FontSize = 13;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Document = new FlowDocument { PagePadding = new Thickness(0) };
    }

    public string? Markdown
    {
        get => (string?)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (MarkdownViewer)d;
        viewer.Document = Build((string?)e.NewValue ?? "", viewer);
    }

    internal static FlowDocument Build(string markdown, FrameworkElement owner)
    {
        var text = (Brush)owner.TryFindResource("Brush.Text") ?? Brushes.Black;
        var accent = (Brush)owner.TryFindResource("Brush.AccentText") ?? Brushes.SaddleBrown;
        var muted = (Brush)owner.TryFindResource("Brush.TextSecondary") ?? Brushes.Gray;

        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = text,
            LineHeight = 20,
        };

        List? list = null;
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0)
            {
                list = null;
                continue;
            }

            var heading = HeadingPattern().Match(line);
            if (heading.Success)
            {
                list = null;
                var level = heading.Groups[1].Value.Length;
                var paragraph = new Paragraph
                {
                    FontSize = level == 1 ? 17 : level == 2 ? 15 : 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = accent,
                    Margin = new Thickness(0, document.Blocks.Count == 0 ? 0 : 10, 0, 4),
                };
                AddInlines(paragraph.Inlines, heading.Groups[2].Value);
                document.Blocks.Add(paragraph);
                continue;
            }

            var bullet = BulletPattern().Match(line);
            var numbered = NumberedPattern().Match(line);
            if (bullet.Success || numbered.Success)
            {
                var ordered = numbered.Success;
                if (list is null || (list.MarkerStyle == TextMarkerStyle.Decimal) != ordered)
                {
                    list = new List
                    {
                        MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                        Margin = new Thickness(0, 2, 0, 2),
                        Padding = new Thickness(18, 0, 0, 0),
                    };
                    document.Blocks.Add(list);
                }
                var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                AddInlines(paragraph.Inlines, (ordered ? numbered : bullet).Groups[1].Value);
                list.ListItems.Add(new ListItem(paragraph));
                continue;
            }

            list = null;
            var isTableRow = line.TrimStart().StartsWith('|');
            if (isTableRow && TableDivider().IsMatch(line))
            {
                continue; // |---|---|
            }
            var block = new Paragraph
            {
                Margin = new Thickness(0, 2, 0, 4),
                Foreground = isTableRow ? muted : text,
                FontFamily = isTableRow ? new FontFamily("Consolas, Segoe UI") : document.FontFamily,
            };
            AddInlines(block.Inlines, isTableRow ? line.Trim().Trim('|').Replace("|", "  │  ") : line);
            document.Blocks.Add(block);
        }
        return document;
    }

    /// <summary>**đậm**, [chữ](link) và link trần.</summary>
    private static void AddInlines(InlineCollection inlines, string text)
    {
        var position = 0;
        foreach (Match match in InlinePattern().Matches(text))
        {
            if (match.Index > position)
            {
                inlines.Add(new Run(text[position..match.Index]));
            }

            if (match.Groups["bold"].Success)
            {
                inlines.Add(new Bold(new Run(match.Groups["bold"].Value)));
            }
            else
            {
                var label = match.Groups["label"].Success ? match.Groups["label"].Value : match.Groups["bare"].Value;
                var url = match.Groups["url"].Success ? match.Groups["url"].Value : match.Groups["bare"].Value;
                inlines.Add(CreateLink(label, url));
            }
            position = match.Index + match.Length;
        }
        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }
    }

    private static Inline CreateLink(string label, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new Run(label);
        }
        var link = new Hyperlink(new Run(label)) { NavigateUri = uri, Cursor = System.Windows.Input.Cursors.Hand };
        link.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        return link;
    }

    [GeneratedRegex(@"^\s{0,3}(#{1,4})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s*[-*•]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\s*\d+[.)]\s+(.*)$")]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"^\s*\|?\s*:?-{2,}")]
    private static partial Regex TableDivider();

    [GeneratedRegex(@"\*\*(?<bold>[^*]+)\*\*|\[(?<label>[^\]]+)\]\((?<url>[^)\s]+)\)|(?<bare>https?://[^\s)\]]+)")]
    private static partial Regex InlinePattern();
}
