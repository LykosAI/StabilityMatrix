using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class InstallerViewModel : PageViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPyRunner pyRunner;
    private readonly IDownloadService downloadService;
    private readonly INotificationService notificationService;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<InstallerViewModel> logger;

    public override string Title => "Add Package";

    [ObservableProperty]
    private BasePackage selectedPackage;

    [ObservableProperty]
    private PackageVersion? selectedVersion;

    [ObservableProperty]
    private ObservableCollection<GitCommit>? availableCommits;

    [ObservableProperty]
    private ObservableCollection<PackageVersion>? availableVersions;

    [ObservableProperty]
    private ObservableCollection<BasePackage> availablePackages;

    [ObservableProperty]
    private GitCommit? selectedCommit;

    [ObservableProperty]
    private string? releaseNotes;

    [ObservableProperty]
    private bool isAdvancedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool showDuplicateWarning;

    [ObservableProperty]
    private bool showIncompatiblePackages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private string? installName;

    [ObservableProperty]
    private SharedFolderMethod selectedSharedFolderMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTorchVersionOptions))]
    private TorchVersion selectedTorchVersion;

    // Version types (release or commit)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseLabelText), nameof(IsReleaseMode), nameof(SelectedVersion))]
    private PackageVersionType selectedVersionType = PackageVersionType.Commit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReleaseModeAvailable))]
    private PackageVersionType availableVersionTypes =
        PackageVersionType.GithubRelease | PackageVersionType.Commit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool isLoading;

    public string ReleaseLabelText => IsReleaseMode ? Resources.Label_Version : Resources.Label_Branch;
    public bool IsReleaseMode
    {
        get => SelectedVersionType == PackageVersionType.GithubRelease;
        set => SelectedVersionType = value ? PackageVersionType.GithubRelease : PackageVersionType.Commit;
    }
    public bool IsReleaseModeAvailable => AvailableVersionTypes.HasFlag(PackageVersionType.GithubRelease);
    public bool ShowTorchVersionOptions => SelectedTorchVersion != TorchVersion.None;

    public bool CanInstall => !string.IsNullOrWhiteSpace(InstallName) && !ShowDuplicateWarning && !IsLoading;

    public IEnumerable<IPackageStep> Steps { get; set; }

    public InstallerViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPyRunner pyRunner,
        IDownloadService downloadService,
        INotificationService notificationService,
        IPrerequisiteHelper prerequisiteHelper,
        ILogger<InstallerViewModel> logger
    )
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.pyRunner = pyRunner;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;

        var filtered = packageFactory.GetAllAvailablePackages().Where(p => p.IsCompatible).ToList();

        AvailablePackages = new ObservableCollection<BasePackage>(
            filtered.Any() ? filtered : packageFactory.GetAllAvailablePackages()
        );
        SelectedPackage = AvailablePackages.FirstOrDefault();
        ShowIncompatiblePackages = !filtered.Any();
    }

    public override void OnLoaded()
    {
        if (AvailablePackages == null)
            return;

        IsReleaseMode = !SelectedPackage.ShouldIgnoreReleases;
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode)
            return;
        // Check for updates
        try
        {
            IsLoading = true;
            var versionOptions = await SelectedPackage.GetAllVersionOptions();
            if (IsReleaseMode)
            {
                AvailableVersions = new ObservableCollection<PackageVersion>(
                    versionOptions.AvailableVersions
                );
                if (!AvailableVersions.Any())
                    return;

                SelectedVersion = AvailableVersions.First(x => !x.IsPrerelease);
            }
            else
            {
                AvailableVersions = new ObservableCollection<PackageVersion>(
                    versionOptions.AvailableBranches
                );
                UpdateSelectedVersionToLatestMain();
            }

            ReleaseNotes = SelectedVersion?.ReleaseNotesMarkdown;
        }
        catch (Exception e)
        {
            logger.LogWarning("Error getting versions: {Exception}", e.ToString());
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Install()
    {
        var result = await notificationService.TryAsync(
            ActuallyInstall(),
            Resources.Label_ErrorInstallingPackage
        );
        if (result.IsSuccessful)
        {
            // OnPrimaryButtonClick();
        }
        else
        {
            var ex = result.Exception!;
            logger.LogError(ex, $"Error installing package: {ex}");

            var dialog = new BetterContentDialog
            {
                Title = Resources.Label_ErrorInstallingPackage,
                Content = ex.ToString(),
                CloseButtonText = Resources.Action_Close
            };
            await dialog.ShowAsync();
        }
    }

    private async Task ActuallyInstall()
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

        var prereqStep = new SetupPrerequisitesStep(prerequisiteHelper, pyRunner, SelectedPackage);

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

        Steps = steps;
    }

    public void Cancel()
    {
        // OnCloseButtonClick();
    }

    partial void OnShowIncompatiblePackagesChanged(bool value)
    {
        var filtered = packageFactory
            .GetAllAvailablePackages()
            .Where(p => ShowIncompatiblePackages || p.IsCompatible)
            .ToList();

        AvailablePackages = new ObservableCollection<BasePackage>(
            filtered.Any() ? filtered : packageFactory.GetAllAvailablePackages()
        );
        SelectedPackage = AvailablePackages[0];
    }

    private void UpdateSelectedVersionToLatestMain()
    {
        if (AvailableVersions is null)
        {
            SelectedVersion = null;
        }
        else
        {
            // First try to find the package-defined main branch
            var version = AvailableVersions.FirstOrDefault(x => x.TagName == SelectedPackage.MainBranch);
            // If not found, try main
            version ??= AvailableVersions.FirstOrDefault(x => x.TagName == "main");

            // If still not found, just use the first one
            version ??= AvailableVersions.FirstOrDefault();
            SelectedVersion = version;
        }
    }

    [RelayCommand]
    private async Task ShowPreview()
    {
        var url = SelectedPackage.PreviewImageUri.ToString();
        var imageStream = await downloadService.GetImageStreamFromUrl(url);
        var bitmap = new Bitmap(imageStream);

        var dialog = new ContentDialog
        {
            PrimaryButtonText = Resources.Action_OpenInBrowser,
            CloseButtonText = Resources.Action_Close,
            Content = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                MaxHeight = 500,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ProcessRunner.OpenUrl(url);
        }
    }

    // When available version types change, reset selected version type if not compatible
    partial void OnAvailableVersionTypesChanged(PackageVersionType value)
    {
        if (!value.HasFlag(SelectedVersionType))
        {
            SelectedVersionType = value;
        }
    }

    // When changing branch / release modes, refresh
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedVersionTypeChanged(PackageVersionType value)
    {
        if (SelectedPackage is null || Design.IsDesignMode)
            return;

        Dispatcher
            .UIThread.InvokeAsync(async () =>
            {
                logger.LogDebug($"Release mode: {IsReleaseMode}");
                var versionOptions = await SelectedPackage.GetAllVersionOptions();

                AvailableVersions = IsReleaseMode
                    ? new ObservableCollection<PackageVersion>(versionOptions.AvailableVersions)
                    : new ObservableCollection<PackageVersion>(versionOptions.AvailableBranches);

                SelectedVersion = AvailableVersions?.FirstOrDefault(x => !x.IsPrerelease);
                if (SelectedVersion is null)
                    return;

                ReleaseNotes = SelectedVersion.ReleaseNotesMarkdown;
                logger.LogDebug($"Loaded release notes for {ReleaseNotes}");

                if (!IsReleaseMode)
                {
                    var commits = (await SelectedPackage.GetAllCommits(SelectedVersion.TagName))?.ToList();
                    if (commits is null || commits.Count == 0)
                        return;

                    AvailableCommits = new ObservableCollection<GitCommit>(commits);
                    SelectedCommit = AvailableCommits.FirstOrDefault();
                    UpdateSelectedVersionToLatestMain();
                }

                InstallName = SelectedPackage.DisplayName;

                IsLoading = false;
            })
            .SafeFireAndForget();
    }

    partial void OnSelectedPackageChanged(BasePackage? value)
    {
        IsLoading = true;
        ReleaseNotes = string.Empty;
        AvailableVersions?.Clear();
        AvailableCommits?.Clear();

        if (value == null)
            return;

        AvailableVersionTypes = SelectedPackage.ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;
        IsReleaseMode = !SelectedPackage.ShouldIgnoreReleases;
        SelectedSharedFolderMethod = SelectedPackage.RecommendedSharedFolderMethod;
        SelectedTorchVersion = SelectedPackage.GetRecommendedTorchVersion();
        SelectedVersionType = SelectedPackage.ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease;

        OnSelectedVersionTypeChanged(SelectedVersionType);
    }

    partial void OnInstallNameChanged(string? value)
    {
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(
            p => p.LibraryPath == $"Packages{Path.DirectorySeparatorChar}{value}"
        );
    }

    partial void OnSelectedVersionChanged(PackageVersion? value)
    {
        ReleaseNotes = value?.ReleaseNotesMarkdown ?? string.Empty;
        if (value == null || Design.IsDesignMode)
            return;

        SelectedCommit = null;
        AvailableCommits?.Clear();

        if (!IsReleaseMode)
        {
            Task.Run(async () =>
                {
                    try
                    {
                        var hashes = await SelectedPackage.GetAllCommits(value.TagName);
                        if (hashes is null)
                            throw new Exception("No commits found");

                        Dispatcher.UIThread.Post(() =>
                        {
                            AvailableCommits = new ObservableCollection<GitCommit>(hashes);
                            SelectedCommit = AvailableCommits[0];
                        });
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, $"Error getting commits: {e.Message}");
                    }
                })
                .SafeFireAndForget();
        }
    }

    public override IconSource IconSource { get; }
}
