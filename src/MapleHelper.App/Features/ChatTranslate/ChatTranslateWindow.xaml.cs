using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MapleHelper.Native;

namespace MapleHelper.App.Features.ChatTranslate;

/// <summary>Ô nhập tiếng Việt nổi trên game. Cửa sổ này phải lấy focus để gõ được, khác với overlay.</summary>
public partial class ChatTranslateWindow : Window
{
    private readonly ChatTranslateViewModel _viewModel;
    private nint _returnFocusTo;

    public ChatTranslateWindow(ChatTranslateViewModel viewModel)
    {
        InitializeComponent();
        // Ô chat giữ nguyên trên màn hình (kể cả khi click sang game để dán), chỉ đóng bằng X hoặc Esc
        DataContext = _viewModel = viewModel;
        Activated += (_, _) => InputBox.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.MakeOverlay(new WindowInteropHelper(this).Handle, noActivate: false);
    }

    /// <summary>Hiện ô nhập, nhớ cửa sổ đang active (thường là game) để trả focus khi xong.</summary>
    public void ShowFor(nint returnFocusTo)
    {
        _returnFocusTo = returnFocusTo;
        _viewModel.Reset();
        Show();
        Activate();
        ForegroundWindow.BringToFront(new WindowInteropHelper(this).Handle);
        InputBox.Focus();
        Keyboard.Focus(InputBox);
    }

    private void CloseAndReturnFocus()
    {
        Hide();
        ForegroundWindow.BringToFront(_returnFocusTo);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            // Đang dịch thì bỏ qua Enter, ô nhập vẫn gõ được để không mất focus
            e.Handled = true;
            if (_viewModel.TranslateCommand.CanExecute(null))
            {
                _viewModel.TranslateCommand.Execute(null);
            }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseAndReturnFocus();
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseAndReturnFocus();
}
