using System.Windows;
using System.Windows.Controls;

namespace MapleHelper.App.Features.News;

public partial class NewsView : UserControl
{
    public NewsView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NewsViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
