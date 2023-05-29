using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class LaunchOptionsDialog : ContentDialog
{
    private readonly LaunchOptionsDialogViewModel viewModel;

    public List<string> AsLaunchArgs() => viewModel.AsLaunchArgs();

    public LaunchOptionsDialog(IContentDialogService dialogService, LaunchOptionsDialogViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void LaunchOptionsDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.OnLoad(); 
    }
}
