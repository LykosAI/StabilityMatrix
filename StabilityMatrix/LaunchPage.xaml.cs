using System.Windows;
using System.Windows.Controls;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix;

public sealed partial class LaunchPage : Page
{
    private readonly LaunchViewModel viewModel;

    public LaunchPage(LaunchViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void LaunchPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.OnLoaded();
        SelectPackageComboBox.ItemsSource = viewModel.InstalledPackages;
    }
}
