using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix;

public sealed partial class FirstLaunchSetupWindow : FluentWindow
{
    public FirstLaunchSetupWindow(FirstLaunchSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void QuitButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Hide();
    }

    private void FirstLaunchSetupWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        (DataContext as FirstLaunchSetupViewModel)!.OnLoaded();
    }
}
