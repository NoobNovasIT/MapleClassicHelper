using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MapleHelper.App.Services;

/// <summary>Lệnh mở link trong trình duyệt, dùng được từ DataTemplate/ResourceDictionary không có code-behind.</summary>
public static class LinkCommands
{
    public static ICommand Open { get; } = new RelayCommand<object?>(parameter =>
    {
        var url = parameter?.ToString();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    });
}
