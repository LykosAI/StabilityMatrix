using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Services;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.ContentDialogControl;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace StabilityMatrix.Helper;

public class DialogFactory : IDialogFactory
{
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;
    private readonly InstallerViewModel installerViewModel;
    private readonly OneClickInstallViewModel oneClickInstallViewModel;
    private readonly SelectInstallLocationsViewModel selectInstallLocationsViewModel;
    private readonly DataDirectoryMigrationViewModel dataDirectoryMigrationViewModel;
    private readonly WebLoginViewModel webLoginViewModel;
    private readonly InstallerWindowDialogService installerWindowDialogService;
    private readonly ISettingsManager settingsManager;

    public DialogFactory(IContentDialogService contentDialogService,
        LaunchOptionsDialogViewModel launchOptionsDialogViewModel,
        ISettingsManager settingsManager, InstallerViewModel installerViewModel,
        OneClickInstallViewModel oneClickInstallViewModel,
        SelectInstallLocationsViewModel selectInstallLocationsViewModel,
        DataDirectoryMigrationViewModel dataDirectoryMigrationViewModel,
        InstallerWindowDialogService installerWindowDialogService,
        WebLoginViewModel webLoginViewModel)
    {
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
        this.installerViewModel = installerViewModel;
        this.oneClickInstallViewModel = oneClickInstallViewModel;
        this.selectInstallLocationsViewModel = selectInstallLocationsViewModel;
        this.dataDirectoryMigrationViewModel = dataDirectoryMigrationViewModel;
        this.webLoginViewModel = webLoginViewModel;
        this.installerWindowDialogService = installerWindowDialogService;
        this.settingsManager = settingsManager;
    }

    public LaunchOptionsDialog CreateLaunchOptionsDialog(IEnumerable<LaunchOptionDefinition> definitions, InstalledPackage installedPackage)
    {
        // Load user settings
        var userLaunchArgs = settingsManager.GetLaunchArgs(installedPackage.Id);
        launchOptionsDialogViewModel.Initialize(definitions, userLaunchArgs);
        return new LaunchOptionsDialog(contentDialogService, launchOptionsDialogViewModel);
    }

    /// <summary>
    /// Creates a dialog that allows the user to enter text for each field name.
    /// Return a list of strings that correspond to the field names.
    /// If cancel is pressed, return null.
    /// <param name="fields">List of (fieldName, placeholder)</param>
    /// </summary>
    public async Task<List<string>?> ShowTextEntryDialog(string title,
        IEnumerable<(string, string)> fields,
        string closeButtonText = "Cancel",
        string saveButtonText = "Save")
    {
        var dialog = contentDialogService.CreateDialog();
        dialog.Title = title;
        dialog.PrimaryButtonAppearance = ControlAppearance.Primary;
        dialog.CloseButtonText = closeButtonText;
        dialog.PrimaryButtonText = saveButtonText;
        dialog.IsPrimaryButtonEnabled = true;

        var textBoxes = new List<TextBox>();
        var stackPanel = new StackPanel();
        dialog.Content = stackPanel;

        foreach (var (fieldName, fieldPlaceholder) in fields)
        {
            var textBox = new TextBox
            {
                PlaceholderText = fieldPlaceholder,
                PlaceholderEnabled = true,
                MinWidth = 200,
            };
            textBoxes.Add(textBox);
            stackPanel.Children.Add(new Card
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = fieldName,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        textBox
                    }
                },
                Margin = new Thickness(16)
            });
        }

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return textBoxes.Select(x => x.Text).ToList();
        }
        return null;
    }

    /// <summary>
    /// Creates and shows a confirmation dialog.
    /// Return true if the user clicks the primary button.
    /// </summary>
    public async Task<bool> ShowConfirmationDialog(string title, string message, string closeButtonText = "Cancel", string primaryButtonText = "Confirm")
    {
        var dialog = contentDialogService.CreateDialog();
        dialog.Title = title;
        dialog.PrimaryButtonAppearance = ControlAppearance.Primary;
        dialog.CloseButtonText = closeButtonText;
        dialog.PrimaryButtonText = primaryButtonText;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.Content = new TextBlock
        {
            Text = message,
            Margin = new Thickness(16)
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public OneClickInstallDialog CreateOneClickInstallDialog()
    {
        return new OneClickInstallDialog(contentDialogService, oneClickInstallViewModel);
    }

    public InstallerWindow CreateInstallerWindow()
    {
        return new InstallerWindow(installerViewModel, installerWindowDialogService);
    }

    public SelectInstallLocationsDialog CreateInstallLocationsDialog()
    {
        var dialog = new SelectInstallLocationsDialog(contentDialogService,
            selectInstallLocationsViewModel)
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false
        };
        return dialog;
    }

    public DataDirectoryMigrationDialog CreateDataDirectoryMigrationDialog()
    {
        var dialog = new DataDirectoryMigrationDialog(contentDialogService,
            dataDirectoryMigrationViewModel)
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false
        };
        return dialog;
    }

    public WebLoginDialog CreateWebLoginDialog()
    {
        return new WebLoginDialog(contentDialogService, webLoginViewModel)
        {
            CloseButtonText = "Cancel",
        };
    }
    
    public SelectModelVersionDialog CreateSelectModelVersionDialog(CivitModel model)
    {
        return new SelectModelVersionDialog(contentDialogService,
            new SelectModelVersionDialogViewModel(model, settingsManager))
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false
        };
    }
}
