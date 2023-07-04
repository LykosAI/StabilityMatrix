using System;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class DataDirectoryMigrationDialog : ContentDialog
{
    private readonly DataDirectoryMigrationViewModel viewModel;

    public DataDirectoryMigrationDialog(IContentDialogService dialogService, DataDirectoryMigrationViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        await viewModel.MigrateCommand.ExecuteAsync(null);
        Hide(ContentDialogResult.Primary);
    }

    private void DataDirectoryMigrationDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        viewModel.OnLoaded().SafeFireAndForget();
    }

    private async void NoThanks_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await Task.Run(viewModel.CleanupOldInstall);
        }
        catch (Exception)
        {
            // ignored
        }

        Hide(ContentDialogResult.Primary);
    }

    private void Back_OnClick(object sender, RoutedEventArgs e)
    {
        Hide(ContentDialogResult.Secondary);
    }
}
