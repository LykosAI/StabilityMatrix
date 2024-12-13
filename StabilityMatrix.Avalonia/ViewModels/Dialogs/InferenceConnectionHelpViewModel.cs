using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(InferenceConnectionHelpDialog))]
[ManagedService]
[RegisterTransient<InferenceConnectionHelpViewModel>]
public partial class InferenceConnectionHelpViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly INavigationService<MainWindowViewModel> navigationService;
    private readonly IPackageFactory packageFactory;
    private readonly RunningPackageService runningPackageService;

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
        INavigationService<MainWindowViewModel> navigationService,
        IPackageFactory packageFactory,
        RunningPackageService runningPackageService
    )
    {
        this.settingsManager = settingsManager;
        this.navigationService = navigationService;
        this.packageFactory = packageFactory;
        this.runningPackageService = runningPackageService;

        // Get comfy type installed packages
        var comfyPackages = this.settingsManager.Settings.InstalledPackages.Where(
            p => p.PackageName == "ComfyUI"
        )
            .ToImmutableArray();

        InstalledPackages = comfyPackages;

        // If no comfy packages, install mode, otherwise launch mode
        if (comfyPackages.Length == 0)
        {
            IsInstallMode = true;
        }
        else
        {
            IsLaunchMode = true;

            // Use active package if its comfy, otherwise use the first comfy type
            if (
                this.settingsManager.Settings.ActiveInstalledPackage is
                { PackageName: "ComfyUI" } activePackage
            )
            {
                SelectedPackage = activePackage;
            }
            else
            {
                SelectedPackage ??= comfyPackages[0];
            }
        }
    }

    /// <summary>
    /// Navigate to the package install page
    /// </summary>
    [RelayCommand]
    private void NavigateToInstall()
    {
        Dispatcher.UIThread.Post(() =>
        {
            navigationService.NavigateTo<PackageManagerViewModel>(
                param: new PackageManagerNavigationOptions
                {
                    OpenInstallerDialog = true,
                    InstallerSelectedPackage = packageFactory
                        .GetAllAvailablePackages()
                        .OfType<ComfyUI>()
                        .First()
                }
            );
        });
    }

    /// <summary>
    /// Request launch of the selected package
    /// </summary>
    [RelayCommand]
    private async Task LaunchSelectedPackage()
    {
        if (SelectedPackage is not null)
        {
            await runningPackageService.StartPackage(SelectedPackage);
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
            PrimaryButtonCommand = IsInstallMode ? NavigateToInstallCommand : LaunchSelectedPackageCommand,
            PrimaryButtonText = IsInstallMode ? Resources.Action_Install : Resources.Action_Launch,
            CloseButtonText = Resources.Action_Close,
            DefaultButton = ContentDialogButton.Primary
        };

        return dialog;
    }
}
