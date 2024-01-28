using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using PackageInstallDetailView = StabilityMatrix.Avalonia.Views.PackageManager.PackageInstallDetailView;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[View(typeof(PackageInstallDetailView))]
public partial class PackageInstallDetailViewModel(
    BasePackage package,
    ISettingsManager settingsManager,
    INotificationService notificationService,
    ILogger<PackageInstallDetailViewModel> logger,
    IPyRunner pyRunner,
    IPrerequisiteHelper prerequisiteHelper,
    INavigationService<NewPackageManagerViewModel> packageNavigationService
) : PageViewModelBase
{
    public BasePackage SelectedPackage { get; } = package;
    public override string Title { get; } = package.DisplayName;
    public override IconSource IconSource => new SymbolIconSource();

    public string FullInstallPath => Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);
    public bool ShowReleaseMode => SelectedPackage.ShouldIgnoreReleases == false;

    public string ReleaseLabelText => IsReleaseMode ? Resources.Label_Version : Resources.Label_Branch;

    public bool ShowTorchVersionOptions => SelectedTorchVersion != TorchVersion.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullInstallPath))]
    private string installName = package.DisplayName;

    [ObservableProperty]
    private bool showDuplicateWarning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseLabelText))]
    private bool isReleaseMode;

    [ObservableProperty]
    private IEnumerable<PackageVersion> availableVersions = new List<PackageVersion>();

    [ObservableProperty]
    private PackageVersion? selectedVersion;

    [ObservableProperty]
    private SharedFolderMethod selectedSharedFolderMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTorchVersionOptions))]
    private TorchVersion selectedTorchVersion;

    [ObservableProperty]
    private ObservableCollection<GitCommit>? availableCommits;

    [ObservableProperty]
    private GitCommit? selectedCommit;

    [ObservableProperty]
    private bool canInstall;

    private PackageVersionOptions? allOptions;

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;

        OnInstallNameChanged(InstallName);

        SelectedTorchVersion = SelectedPackage.GetRecommendedTorchVersion();
        SelectedSharedFolderMethod = SelectedPackage.RecommendedSharedFolderMethod;

        allOptions = await SelectedPackage.GetAllVersionOptions();
        if (ShowReleaseMode)
        {
            IsReleaseMode = true;
        }
        else
        {
            UpdateVersions();
            await UpdateCommits(SelectedPackage.MainBranch);
        }

        CanInstall = !ShowDuplicateWarning;
    }

    [RelayCommand]
    private async Task Install()
    {
        if (string.IsNullOrWhiteSpace(InstallName))
        {
            notificationService.Show(
                new Notification(
                    "Package name is empty",
                    "Please enter a name for the package",
                    NotificationType.Error
                )
            );
            return;
        }

        var setPackageInstallingStep = new SetPackageInstallingStep(settingsManager, InstallName);

        var installLocation = Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);
        if (Directory.Exists(installLocation))
        {
            var installPath = new DirectoryPath(installLocation);
            await installPath.DeleteVerboseAsync(logger);
        }

        var prereqStep = new SetupPrerequisitesStep(prerequisiteHelper, pyRunner);

        var downloadOptions = new DownloadPackageVersionOptions();
        var installedVersion = new InstalledPackageVersion();
        if (IsReleaseMode)
        {
            downloadOptions.VersionTag =
                SelectedVersion?.TagName ?? throw new NullReferenceException("Selected version is null");
            downloadOptions.IsLatest = AvailableVersions?.First().TagName == downloadOptions.VersionTag;
            downloadOptions.IsPrerelease = SelectedVersion.IsPrerelease;

            installedVersion.InstalledReleaseVersion = downloadOptions.VersionTag;
            installedVersion.IsPrerelease = SelectedVersion.IsPrerelease;
        }
        else
        {
            downloadOptions.CommitHash =
                SelectedCommit?.Sha ?? throw new NullReferenceException("Selected commit is null");
            downloadOptions.BranchName =
                SelectedVersion?.TagName ?? throw new NullReferenceException("Selected version is null");
            downloadOptions.IsLatest = AvailableCommits?.First().Sha == SelectedCommit.Sha;

            installedVersion.InstalledBranch =
                SelectedVersion?.TagName ?? throw new NullReferenceException("Selected version is null");
            installedVersion.InstalledCommitSha = downloadOptions.CommitHash;
        }

        var downloadStep = new DownloadPackageVersionStep(SelectedPackage, installLocation, downloadOptions);
        var installStep = new InstallPackageStep(
            SelectedPackage,
            SelectedTorchVersion,
            SelectedSharedFolderMethod,
            downloadOptions,
            installLocation
        );

        var setupModelFoldersStep = new SetupModelFoldersStep(
            SelectedPackage,
            SelectedSharedFolderMethod,
            installLocation
        );

        var package = new InstalledPackage
        {
            DisplayName = InstallName,
            LibraryPath = Path.Combine("Packages", InstallName),
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            Version = installedVersion,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now,
            PreferredTorchVersion = SelectedTorchVersion,
            PreferredSharedFolderMethod = SelectedSharedFolderMethod
        };

        var addInstalledPackageStep = new AddInstalledPackageStep(settingsManager, package);

        var steps = new List<IPackageStep>
        {
            setPackageInstallingStep,
            prereqStep,
            downloadStep,
            installStep,
            setupModelFoldersStep,
            addInstalledPackageStep
        };

        var packageName = SelectedPackage.Name;

        var runner = new PackageModificationRunner
        {
            ModificationCompleteMessage = $"Installed {packageName} at [{installLocation}]",
            ModificationFailedMessage = $"Could not install {packageName}",
            ShowDialogOnStart = true
        };
        runner.Completed += (_, completedRunner) =>
        {
            notificationService.OnPackageInstallCompleted(completedRunner);
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps.ToList());

        if (!runner.Failed)
        {
            EventManager.Instance.OnInstalledPackagesChanged();
        }
    }

    private void UpdateVersions()
    {
        CanInstall = false;

        AvailableVersions =
            IsReleaseMode && ShowReleaseMode ? allOptions.AvailableVersions : allOptions.AvailableBranches;

        SelectedVersion = !IsReleaseMode
            ? AvailableVersions?.FirstOrDefault(x => x.TagName == SelectedPackage.MainBranch)
                ?? AvailableVersions?.FirstOrDefault()
            : AvailableVersions?.FirstOrDefault();

        CanInstall = !ShowDuplicateWarning;
    }

    private async Task UpdateCommits(string branchName)
    {
        CanInstall = false;

        var commits = await SelectedPackage.GetAllCommits(branchName);
        if (commits != null)
        {
            AvailableCommits = new ObservableCollection<GitCommit>(commits);
            SelectedCommit = AvailableCommits.FirstOrDefault();
        }

        CanInstall = !ShowDuplicateWarning;
    }

    partial void OnInstallNameChanged(string? value)
    {
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(
            p => p.LibraryPath == $"Packages{Path.DirectorySeparatorChar}{value}"
        );
        CanInstall = !ShowDuplicateWarning;
    }

    partial void OnIsReleaseModeChanged(bool value)
    {
        UpdateVersions();
    }

    partial void OnSelectedVersionChanged(PackageVersion? value)
    {
        if (IsReleaseMode)
            return;

        UpdateCommits(value?.TagName ?? SelectedPackage.MainBranch).SafeFireAndForget();
    }
}
