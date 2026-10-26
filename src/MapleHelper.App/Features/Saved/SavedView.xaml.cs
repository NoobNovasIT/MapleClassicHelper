using System.Windows;
using System.Windows.Controls;

namespace MapleHelper.App.Features.Saved;

public partial class SavedView : UserControl
{
    public SavedView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SavedViewModel viewModel)
        {
            await viewModel.ReloadAsync();
        }
    }
}
