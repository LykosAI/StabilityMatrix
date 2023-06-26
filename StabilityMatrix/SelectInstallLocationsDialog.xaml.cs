using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class SelectInstallLocationsDialog : ContentDialog
{
    private readonly SelectInstallLocationsViewModel viewModel;

    public SelectInstallLocationsDialog(IContentDialogService dialogService, SelectInstallLocationsViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
