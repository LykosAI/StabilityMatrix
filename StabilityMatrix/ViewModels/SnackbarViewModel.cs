using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Common;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using SymbolIcon = Wpf.Ui.Controls.IconElements.SymbolIcon;

namespace StabilityMatrix.ViewModels;

public partial class SnackbarViewModel : ObservableObject
{
    private readonly ISnackbarService snackbarService;

    [ObservableProperty]
    private ControlAppearance snackbarAppearance = ControlAppearance.Secondary;

    [ObservableProperty]
    private int snackbarTimeout = 2000;

    public SnackbarViewModel(ISnackbarService snackbarService)
    {
        this.snackbarService = snackbarService;
    }

    [RelayCommand]
    private void OnOpenSnackbar(object sender)
    {
        snackbarService.Timeout = SnackbarTimeout;
        snackbarService.Show("Some title.", "Some message.", new SymbolIcon(SymbolRegular.Fluent24), SnackbarAppearance);
    }
}
