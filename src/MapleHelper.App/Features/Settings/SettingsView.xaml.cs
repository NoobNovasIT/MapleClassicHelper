using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace MapleHelper.App.Features.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    // PasswordBox.Password không bind được (cố ý vì bảo mật), nên đồng bộ hai chiều bằng code-behind
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && vm.ApiKey != ApiKeyBox.Password)
        {
            vm.ApiKey = ApiKeyBox.Password;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SettingsViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is SettingsViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            ApiKeyBox.Password = newVm.ApiKey;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ApiKey) && ViewModel is { } vm && ApiKeyBox.Password != vm.ApiKey)
        {
            ApiKeyBox.Password = vm.ApiKey;
        }
    }
}
