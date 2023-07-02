using System;
using System.IO;
using System.Windows;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;

namespace StabilityMatrix;

public partial class DataDirectoryMigrationDialog : ContentDialog
{
    public DataDirectoryMigrationDialog(IContentDialogService dialogService, DataDirectoryMigrationViewModel viewModel) : base(
        dialogService.GetContentPresenter())
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ((DataDirectoryMigrationViewModel) DataContext).MigrateCommand.ExecuteAsync(null);
        Hide(ContentDialogResult.Primary);
    }

    private void DataDirectoryMigrationDialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        ((DataDirectoryMigrationViewModel) DataContext).OnLoaded();
    }

    private void NoThanks_OnClick(object sender, RoutedEventArgs e)
    {
        var oldSettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StabilityMatrix", "settings.json");
        File.Move(oldSettingsPath, oldSettingsPath + ".old", true);
        
        Hide(ContentDialogResult.Primary);
    }
}
