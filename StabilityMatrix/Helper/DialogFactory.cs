using System;
using StabilityMatrix.Models;
using StabilityMatrix.ViewModels;
using Wpf.Ui.Contracts;

namespace StabilityMatrix.Helper;

public class DialogFactory : IDialogFactory
{
    private readonly IContentDialogService contentDialogService;
    private readonly LaunchOptionsDialogViewModel launchOptionsDialogViewModel;
    private readonly ISettingsManager settingsManager;

    public DialogFactory(IContentDialogService contentDialogService, LaunchOptionsDialogViewModel launchOptionsDialogViewModel, ISettingsManager settingsManager)
    {
        this.contentDialogService = contentDialogService;
        this.launchOptionsDialogViewModel = launchOptionsDialogViewModel;
        this.settingsManager = settingsManager;
    }
    
    public LaunchOptionsDialog CreateLaunchOptionsDialog(BasePackage selectedPackage, InstalledPackage installedPackage)
    {
        var definitions = selectedPackage.LaunchOptions;
        launchOptionsDialogViewModel.SelectedPackage = selectedPackage;
        launchOptionsDialogViewModel.Cards.Clear();
        // Create cards
        launchOptionsDialogViewModel.CardsFromDefinitions(definitions);
        // Load user settings
        var userLaunchArgs = settingsManager.GetLaunchArgs(installedPackage.Id);
        launchOptionsDialogViewModel.LoadFromLaunchArgs(userLaunchArgs);

        return new LaunchOptionsDialog(contentDialogService, launchOptionsDialogViewModel);
    }
}
