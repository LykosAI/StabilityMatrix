using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(InferenceConnectionHelpDialog))]
public partial class InferenceConnectionHelpViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly INavigationService navigationService;
    private readonly IPackageFactory packageFactory;

    [ObservableProperty]
    private string title = "Hello";

    [ObservableProperty]
    private IReadOnlyList<InstalledPackage> installedPackages = Array.Empty<InstalledPackage>();

    [ObservableProperty]
    private InstalledPackage? selectedPackage;

    [ObservableProperty]
    private bool isFirstTimeWelcome;

    /// <summary>
    /// When the user has no Comfy packages, and we need to prompt to install
    /// </summary>
    [ObservableProperty]
    private bool isInstallMode;

    /// <summary>
    /// When the user has Comfy packages, and we need to prompt to launch
    /// </summary>
    [ObservableProperty]
    private bool isLaunchMode;

    public InferenceConnectionHelpViewModel(
        ISettingsManager settingsManager,
        INavigationService navigationService,
        IPackageFactory packageFactory
    )
    {
        this.settingsManager = settingsManager;
        this.navigationService = navigationService;
        this.packageFactory = packageFactory;

        // Get comfy type installed packages
        var comfyPackages = this.settingsManager.Settings.InstalledPackages
            .Where(p => p.PackageName == "ComfyUI")
            .ToImmutableArray();

        InstalledPackages = comfyPackages;

        // If there are none, use install mode
        if (comfyPackages.Length == 0)
        {
            IsInstallMode = true;
        }
        // Otherwise launch mode
        else
        {
            SelectedPackage = this.settingsManager.Settings.ActiveInstalledPackage;
            SelectedPackage ??= comfyPackages[0];
            IsLaunchMode = true;
        }
    }

    /// <summary>
    /// Navigate to the package install page
    /// </summary>
    [RelayCommand]
    private void NavigateToInstall()
    {
        navigationService.NavigateTo<PackageManagerViewModel>(
            param: new PackageManagerPage.PackageManagerNavigationOptions
            {
                OpenInstallerDialog = true,
                InstallerSelectedPackage = packageFactory
                    .GetAllAvailablePackages()
                    .First(p => p is ComfyUI)
            }
        );
    }

    /// <summary>
    /// Request launch of the selected package
    /// </summary>
    [RelayCommand]
    private void LaunchSelectedPackage()
    {
        if (SelectedPackage?.Id is { } id)
        {
            EventManager.Instance.OnPackageLaunchRequested(id);
        }
    }

    /// <summary>
    /// Create a better content dialog for this view model
    /// </summary>
    public BetterContentDialog CreateDialog()
    {
        var dialog = new BetterContentDialog
        {
            Content = new InferenceConnectionHelpDialog { DataContext = this },
            PrimaryButtonCommand = IsInstallMode
                ? NavigateToInstallCommand
                : LaunchSelectedPackageCommand,
            PrimaryButtonText = IsInstallMode ? "Install" : "Launch",
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Primary
        };

        return dialog;
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();

        // Check if
    }
}
