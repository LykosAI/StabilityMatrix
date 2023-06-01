using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class OneClickInstallDialog : ContentDialog
{
    private readonly OneClickInstallViewModel viewModel;

    public OneClickInstallDialog(IContentDialogService dialogService, OneClickInstallViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OneClickInstallDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        //viewModel.OnLoad(); 
    }
}
