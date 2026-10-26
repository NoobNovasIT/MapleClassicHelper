using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MapleHelper.Native;

namespace MapleHelper.App.Features.Results;

public partial class ResultPopupWindow : Window
{
    private readonly ResultPopupViewModel _viewModel;

    public ResultPopupWindow(ResultPopupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Không hiện trên Alt+Tab, luôn nằm trên game
        WindowStyles.MakeOverlay(new WindowInteropHelper(this).Handle, noActivate: false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.RequeryCommand.CanExecute(null))
        {
            e.Handled = true;
            _viewModel.RequeryCommand.Execute(null);
        }
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
