using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[ManagedService]
[Transient]
public partial class OneClickInstallViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<OneClickInstallViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly INavigationService navigationService;
    private const string DefaultPackageName = "stable-diffusion-webui";

    [ObservableProperty]
    private string headerText;

    [ObservableProperty]
    private string subHeaderText;

    [ObservableProperty]
    private string subSubHeaderText = string.Empty;

    [ObservableProperty]
    private bool showInstallButton;

    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private bool showIncompatiblePackages;

    [ObservableProperty]
    private ObservableCollection<BasePackage> allPackages;

    [ObservableProperty]
    private BasePackage selectedPackage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressBarVisible))]
    private int oneClickInstallProgress;

    private bool isInferenceInstall;

    public bool IsProgressBarVisible => OneClickInstallProgress > 0 || IsIndeterminate;

    public OneClickInstallViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPrerequisiteHelper prerequisiteHelper,
        ILogger<OneClickInstallViewModel> logger,
        IPyRunner pyRunner,
        INavigationService navigationService
    )
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.navigationService = navigationService;

        HeaderText = Resources.Text_WelcomeToStabilityMatrix;
        SubHeaderText = Resources.Text_OneClickInstaller_SubHeader;
        ShowInstallButton = true;

        var filteredPackages = this.packageFactory
            .GetAllAvailablePackages()
            .Where(p => p is { OfferInOneClickInstaller: true, IsCompatible: true })
            .ToList();

        AllPackages = new ObservableCollection<BasePackage>(
            filteredPackages.Any()
                ? filteredPackages
                : this.packageFactory.GetAllAvailablePackages()
        );
        SelectedPackage = AllPackages[0];
    }

    [RelayCommand]
    private async Task Install()
    {
        ShowInstallButton = false;
        await DoInstall();
        ShowInstallButton = true;
    }

    [RelayCommand]
    private Task ToggleAdvancedMode()
    {
        EventManager.Instance.OnOneClickInstallFinished(true);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task InstallComfyForInference()
    {
        var comfyPackage = AllPackages.FirstOrDefault(x => x is ComfyUI);
        if (comfyPackage != null)
        {
            SelectedPackage = comfyPackage;
            isInferenceInstall = true;
            await InstallCommand.ExecuteAsync(null);
        }
    }

    private async Task DoInstall()
    {
        var steps = new List<IPackageStep>
        {
            new SetPackageInstallingStep(settingsManager, SelectedPackage.Name),
            new SetupPrerequisitesStep(prerequisiteHelper, pyRunner)
        };

        // get latest version & download & install
        var installLocation = Path.Combine(
            settingsManager.LibraryDir,
            "Packages",
            SelectedPackage.Name
        );
        if (Directory.Exists(installLocation))
        {
            var installPath = new DirectoryPath(installLocation);
            await installPath.DeleteVerboseAsync();
        }

        var downloadVersion = await SelectedPackage.GetLatestVersion();
        var installedVersion = new InstalledPackageVersion { IsPrerelease = false };

        if (SelectedPackage.ShouldIgnoreReleases)
        {
            installedVersion.InstalledBranch = downloadVersion.BranchName;
            installedVersion.InstalledCommitSha = downloadVersion.CommitHash;
        }
        else
        {
            installedVersion.InstalledReleaseVersion = downloadVersion.VersionTag;
        }

        var torchVersion = SelectedPackage.GetRecommendedTorchVersion();
        var recommendedSharedFolderMethod = SelectedPackage.RecommendedSharedFolderMethod;

        var downloadStep = new DownloadPackageVersionStep(
            SelectedPackage,
            installLocation,
            downloadVersion
        );
        steps.Add(downloadStep);

        var installStep = new InstallPackageStep(
            SelectedPackage,
            torchVersion,
            recommendedSharedFolderMethod,
            downloadVersion,
            installLocation
        );
        steps.Add(installStep);

        var setupModelFoldersStep = new SetupModelFoldersStep(
            SelectedPackage,
            recommendedSharedFolderMethod,
            installLocation
        );
        steps.Add(setupModelFoldersStep);

        var installedPackage = new InstalledPackage
        {
            DisplayName = SelectedPackage.DisplayName,
            LibraryPath = Path.Combine("Packages", SelectedPackage.Name),
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            Version = installedVersion,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now,
            PreferredTorchVersion = torchVersion,
            PreferredSharedFolderMethod = recommendedSharedFolderMethod
        };

        var addInstalledPackageStep = new AddInstalledPackageStep(
            settingsManager,
            installedPackage
        );
        steps.Add(addInstalledPackageStep);

        var runner = new PackageModificationRunner
        {
            ShowDialogOnStart = true,
            HideCloseButton = true,
        };
        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        EventManager.Instance.OnInstalledPackagesChanged();
        HeaderText = $"{SelectedPackage.DisplayName} installed successfully";
        for (var i = 3; i > 0; i--)
        {
            SubHeaderText = $"{Resources.Text_ProceedingToLaunchPage} ({i}s)";
            await Task.Delay(1000);
        }

        // should close dialog
        EventManager.Instance.OnOneClickInstallFinished(false);
        if (isInferenceInstall)
        {
            navigationService.NavigateTo<InferenceViewModel>();
        }
    }

    partial void OnShowIncompatiblePackagesChanged(bool value)
    {
        var filteredPackages = packageFactory
            .GetAllAvailablePackages()
            .Where(p => p.OfferInOneClickInstaller && (ShowIncompatiblePackages || p.IsCompatible))
            .ToList();

        AllPackages = new ObservableCollection<BasePackage>(
            filteredPackages.Any() ? filteredPackages : packageFactory.GetAllAvailablePackages()
        );
        SelectedPackage = AllPackages[0];
    }
}
