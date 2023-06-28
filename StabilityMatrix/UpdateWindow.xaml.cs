using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix;

public partial class UpdateWindow : FluentWindow
{
    private readonly UpdateWindowViewModel viewModel;

    public UpdateWindow(UpdateWindowViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void UpdateWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await viewModel.OnLoaded();
    }

    private void RemindMeLaterButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
