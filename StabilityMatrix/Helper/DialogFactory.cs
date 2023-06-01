using System.Collections.Generic;
using StabilityMatrix.Models;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;

namespace StabilityMatrix.Helper;

public class DialogFactory : IDialogFactory
{
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;
    private readonly InstallerViewModel installerViewModel;
    private readonly OneClickInstallViewModel oneClickInstallViewModel;
    private readonly ISettingsManager settingsManager;

    public DialogFactory(IContentDialogService contentDialogService, LaunchOptionsDialogViewModel launchOptionsDialogViewModel, 
        ISettingsManager settingsManager, InstallerViewModel installerViewModel, OneClickInstallViewModel oneClickInstallViewModel)
    {
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
        this.installerViewModel = installerViewModel;
        this.oneClickInstallViewModel = oneClickInstallViewModel;
        this.settingsManager = settingsManager;
    }

    public LaunchOptionsDialog CreateLaunchOptionsDialog(IEnumerable<LaunchOptionDefinition> definitions, InstalledPackage installedPackage)
    {
        // Load user settings
        var userLaunchArgs = settingsManager.GetLaunchArgs(installedPackage.Id);
        launchOptionsDialogViewModel.Initialize(definitions, userLaunchArgs);
        return new LaunchOptionsDialog(contentDialogService, launchOptionsDialogViewModel);
    }
    
    public OneClickInstallDialog CreateOneClickInstallDialog()
    {
        return new OneClickInstallDialog(contentDialogService, oneClickInstallViewModel);
    }
    
    public InstallerWindow CreateInstallerWindow()
    {
        return new InstallerWindow(installerViewModel);
    }
}
