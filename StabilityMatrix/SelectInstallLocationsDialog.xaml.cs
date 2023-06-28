using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class SelectInstallLocationsDialog : ContentDialog
{
    public SelectInstallLocationsDialog(IContentDialogService dialogService, SelectInstallLocationsViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide(ContentDialogResult.Primary);
    }

    private void SelectInstallLocationsDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        ((SelectInstallLocationsViewModel) DataContext).OnLoaded();
    }
}
