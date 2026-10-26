using System.Windows;
using System.Windows.Controls;

namespace MapleHelper.App.Features.History;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel viewModel)
        {
            await viewModel.ReloadAsync();
        }
    }
}
