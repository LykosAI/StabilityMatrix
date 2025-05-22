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
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.PackageSteps;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Helper.Factory;
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
    IPrerequisiteHelper prerequisiteHelper,
    INavigationService<PackageManagerViewModel> packageNavigationService,
    IPackageFactory packageFactory,
    IAnalyticsHelper analyticsHelper,
    IPyInstallationManager pyInstallationManager
) : PageViewModelBase
{
    public BasePackage SelectedPackage { get; } = package;
    public override string Title { get; } = package.DisplayName;
    public override IconSource IconSource => new SymbolIconSource();

    public string FullInstallPath => Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);
    public bool ShowReleaseMode => SelectedPackage.ShouldIgnoreReleases == false;

    public string? ReleaseTooltipText =>
        ShowReleaseMode ? null : Resources.Label_ReleasesUnavailableForThisPackage;

    public bool ShowTorchIndexOptions => SelectedTorchIndex != TorchIndex.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullInstallPath))]
    private string installName = package.DisplayName;

    [ObservableProperty]
    private bool showDuplicateWarning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseTooltipText))]
    private bool isReleaseMode;

    [ObservableProperty]
    private IEnumerable<PackageVersion> availableVersions = new List<PackageVersion>();

    [ObservableProperty]
    private PackageVersion? selectedVersion;

    [ObservableProperty]
    private SharedFolderMethod selectedSharedFolderMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTorchIndexOptions))]
    private TorchIndex selectedTorchIndex;

    [ObservableProperty]
    private ObservableCollection<GitCommit>? availableCommits;

    [ObservableProperty]
    private GitCommit? selectedCommit;

    [ObservableProperty]
    private bool isOutputSharingEnabled = true;

    [ObservableProperty]
    private bool canInstall;

    [ObservableProperty]
    private ObservableCollection<UvPythonInfo> availablePythonVersions = new();

    [ObservableProperty]
    private UvPythonInfo selectedPythonVersion;

    public PythonPackageSpecifiersViewModel PythonPackageSpecifiersViewModel { get; } =
        new() { Title = null };

    private PackageVersionOptions? allOptions;

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        OnInstallNameChanged(InstallName);

        CanInstall = false;

        SelectedTorchIndex = SelectedPackage.GetRecommendedTorchVersion();
        SelectedSharedFolderMethod = SelectedPackage.RecommendedSharedFolderMethod;

        // Initialize Python versions
        await prerequisiteHelper.InstallUvIfNecessary();
        var pythonVersions = await pyInstallationManager.GetAllAvailablePythonsAsync();
        AvailablePythonVersions = new ObservableCollection<UvPythonInfo>(pythonVersions);
        SelectedPythonVersion = GetRecommendedPyVersion() ?? AvailablePythonVersions.LastOrDefault();

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

        if (SelectedPackage.InstallRequiresAdmin)
        {
            var reason = $"""
                # **{SelectedPackage.DisplayName}** may require administrator privileges during the installation. If necessary, you will be prompted to allow the installer to run with elevated privileges.

                ## The reason for this requirement is:
                {SelectedPackage.AdminRequiredReason}

                ## Would you like to proceed?
                """;
            var dialog = DialogHelper.CreateMarkdownDialog(reason, string.Empty);
            dialog.PrimaryButtonText = Resources.Action_Yes;
            dialog.CloseButtonText = Resources.Action_Cancel;
            dialog.IsPrimaryButtonEnabled = true;
            dialog.DefaultButton = ContentDialogButton.Primary;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }
        }

        if (SelectedPackage is StableSwarm)
        {
            var comfy = settingsManager.Settings.InstalledPackages.FirstOrDefault(x =>
                x.PackageName is nameof(ComfyUI) or "ComfyUI-Zluda"
            );

            if (comfy == null)
            {
                // show dialog to install comfy
                var dialog = new BetterContentDialog
                {
                    Title = Resources.Label_ComfyRequiredTitle,
                    Content = Resources.Label_ComfyRequiredDetail,
                    PrimaryButtonText = Resources.Action_Yes,
                    CloseButtonText = Resources.Label_No,
                    DefaultButton = ContentDialogButton.Primary,
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                    return;

                packageNavigationService.GoBack();
                var comfyPackage = packageFactory.FindPackageByName(nameof(ComfyUI));
                if (comfyPackage is null)
                    return;

                var vm = new PackageInstallDetailViewModel(
                    comfyPackage,
                    settingsManager,
                    notificationService,
                    logger,
                    prerequisiteHelper,
                    packageNavigationService,
                    packageFactory,
                    analyticsHelper,
                    pyInstallationManager
                );
                packageNavigationService.NavigateTo(vm);
                return;
            }
        }

        InstallName = InstallName.Trim();

        var installLocation = Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);
        if (Directory.Exists(installLocation))
        {
            var installPath = new DirectoryPath(installLocation);
            await installPath.DeleteVerboseAsync(logger);
        }

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

        var pipOverrides = PythonPackageSpecifiersViewModel.GetSpecifiers().ToList();

        var package = new InstalledPackage
        {
            DisplayName = InstallName,
            LibraryPath = Path.Combine("Packages", InstallName),
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            Version = installedVersion,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now,
            PreferredTorchIndex = SelectedTorchIndex,
            PreferredSharedFolderMethod = SelectedSharedFolderMethod,
            UseSharedOutputFolder = IsOutputSharingEnabled,
            PipOverrides = pipOverrides.Count > 0 ? pipOverrides : null,
            PythonVersion = SelectedPythonVersion.Version.StringValue,
        };

        var steps = new List<IPackageStep>
        {
            new SetPackageInstallingStep(settingsManager, InstallName),
            new SetupPrerequisitesStep(prerequisiteHelper, SelectedPackage, SelectedPythonVersion.Version),
            new DownloadPackageVersionStep(
                SelectedPackage,
                installLocation,
                new DownloadPackageOptions { VersionOptions = downloadOptions }
            ),
            new UnpackSiteCustomizeStep(Path.Combine(installLocation, "venv")),
            new InstallPackageStep(
                SelectedPackage,
                installLocation,
                package,
                new InstallPackageOptions
                {
                    SharedFolderMethod = SelectedSharedFolderMethod,
                    VersionOptions = downloadOptions,
                    PythonOptions =
                    {
                        TorchIndex = SelectedTorchIndex,
                        PythonVersion = SelectedPythonVersion.Version,
                    },
                }
            ),
            new SetupModelFoldersStep(SelectedPackage, SelectedSharedFolderMethod, installLocation),
        };

        if (IsOutputSharingEnabled)
        {
            steps.Add(new SetupOutputSharingStep(SelectedPackage, installLocation));
        }

        steps.Add(new AddInstalledPackageStep(settingsManager, package));

        var packageName = SelectedPackage.Name;

        var runner = new PackageModificationRunner
        {
            ModificationCompleteMessage = $"Installed {packageName} at [{installLocation}]",
            ModificationFailedMessage = $"Could not install {packageName}",
            ShowDialogOnStart = true,
        };
        runner.Completed += (_, completedRunner) =>
        {
            notificationService.OnPackageInstallCompleted(completedRunner);
        };

        EventManager.Instance.OnPackageInstallProgressAdded(runner);
        await runner.ExecuteSteps(steps);

        if (!runner.Failed)
        {
            if (ReferenceEquals(this, packageNavigationService.CurrentPageDataContext))
            {
                packageNavigationService.GoBack();
                packageNavigationService.GoBack();
                await Task.Delay(100);
            }

            EventManager.Instance.OnInstalledPackagesChanged();
        }

        analyticsHelper
            .TrackPackageInstallAsync(packageName, installedVersion.DisplayVersion, !runner.Failed)
            .SafeFireAndForget();
    }

    private void UpdateVersions()
    {
        CanInstall = false;

        AvailableVersions =
            IsReleaseMode && ShowReleaseMode ? allOptions.AvailableVersions : allOptions.AvailableBranches;

        SelectedVersion = !IsReleaseMode
            ? AvailableVersions?.FirstOrDefault(x => x.TagName == SelectedPackage.MainBranch)
                ?? AvailableVersions?.FirstOrDefault()
            : AvailableVersions?.FirstOrDefault(v => !v.IsPrerelease);

        CanInstall = !ShowDuplicateWarning;
    }

    private async Task UpdateCommits(string branchName)
    {
        CanInstall = false;

        var commits = await SelectedPackage.GetAllCommits(branchName);
        if (commits != null)
        {
            AvailableCommits = new ObservableCollection<GitCommit>(
                [.. commits, new GitCommit { Sha = "Custom " }]
            );
        }
        else
        {
            AvailableCommits = [new GitCommit { Sha = "Custom " }];
        }

        SelectedCommit = AvailableCommits.FirstOrDefault();
        CanInstall = !ShowDuplicateWarning;
    }

    partial void OnInstallNameChanged(string? value)
    {
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(p =>
            p.LibraryPath == $"Packages{Path.DirectorySeparatorChar}{value}"
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

    async partial void OnSelectedCommitChanged(GitCommit? oldValue, GitCommit? newValue)
    {
        if (newValue is not { Sha: "Custom " })
            return;

        List<TextBoxField> textBoxFields = [new() { Label = "Commit hash", MinWidth = 400 }];
        var dialog = DialogHelper.CreateTextEntryDialog("Enter a commit hash", string.Empty, textBoxFields);
        var dialogResult = await dialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
        {
            SelectedCommit = oldValue;
            return;
        }

        var commitHash = textBoxFields[0].Text;
        if (string.IsNullOrWhiteSpace(commitHash))
        {
            SelectedCommit = oldValue;
            return;
        }

        var commit = new GitCommit { Sha = commitHash };
        AvailableCommits?.Insert(AvailableCommits.IndexOf(newValue), commit);
        SelectedCommit = commit;
    }

    private UvPythonInfo? GetRecommendedPyVersion() =>
        AvailablePythonVersions.FirstOrDefault(x =>
            x.Version.Equals(SelectedPackage.RecommendedPythonVersion)
        );
}
