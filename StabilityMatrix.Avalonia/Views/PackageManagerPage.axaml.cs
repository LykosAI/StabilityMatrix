using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using StabilityMatrix.Avalonia.ViewModels;

namespace StabilityMatrix.Avalonia.Views;

public partial class PackageManagerPage : UserControl
{
    public PackageManagerPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PackageManagerViewModel viewModel)
        {
            await viewModel.OnLoaded();
        }
    }
}
