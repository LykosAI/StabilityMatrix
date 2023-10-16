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
using NLog;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class InstallerViewModel : ContentDialogViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly ISettingsManager settingsManager;
    private readonly IPyRunner pyRunner;
    private readonly IDownloadService downloadService;
    private readonly INotificationService notificationService;
    private readonly IPrerequisiteHelper prerequisiteHelper;

    [ObservableProperty]
    private BasePackage selectedPackage;

    [ObservableProperty]
    private PackageVersion? selectedVersion;

    [ObservableProperty]
    private IReadOnlyList<BasePackage>? availablePackages;

    [ObservableProperty]
    private ObservableCollection<GitCommit>? availableCommits;

    [ObservableProperty]
    private ObservableCollection<PackageVersion>? availableVersions;

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
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private string? installName;

    [ObservableProperty]
    private SharedFolderMethod selectedSharedFolderMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTorchVersionOptions))]
    private TorchVersion selectedTorchVersion;

    // Version types (release or commit)
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ReleaseLabelText),
        nameof(IsReleaseMode),
        nameof(SelectedVersion)
    )]
    private PackageVersionType selectedVersionType = PackageVersionType.Commit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReleaseModeAvailable))]
    private PackageVersionType availableVersionTypes =
        PackageVersionType.GithubRelease | PackageVersionType.Commit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool isLoading;

    public string ReleaseLabelText =>
        IsReleaseMode ? Resources.Label_Version : Resources.Label_Branch;
    public bool IsReleaseMode
    {
        get => SelectedVersionType == PackageVersionType.GithubRelease;
        set =>
            SelectedVersionType = value
                ? PackageVersionType.GithubRelease
                : PackageVersionType.Commit;
    }
    public bool IsReleaseModeAvailable =>
        AvailableVersionTypes.HasFlag(PackageVersionType.GithubRelease);
    public bool ShowTorchVersionOptions => SelectedTorchVersion != TorchVersion.None;

    public bool CanInstall =>
        !string.IsNullOrWhiteSpace(InstallName) && !ShowDuplicateWarning && !IsLoading;

    public ProgressViewModel InstallProgress { get; } = new();
    public IEnumerable<IPackageStep> Steps { get; set; }

    public InstallerViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPyRunner pyRunner,
        IDownloadService downloadService,
        INotificationService notificationService,
        IPrerequisiteHelper prerequisiteHelper
    )
    {
        this.settingsManager = settingsManager;
        this.pyRunner = pyRunner;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.prerequisiteHelper = prerequisiteHelper;

        // AvailablePackages and SelectedPackage
        AvailablePackages = new ObservableCollection<BasePackage>(
            packageFactory.GetAllAvailablePackages()
        );
        SelectedPackage = AvailablePackages[0];
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
            Logger.Warn("Error getting versions: {Exception}", e.ToString());
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
            OnPrimaryButtonClick();
        }
        else
        {
            var ex = result.Exception!;
            Logger.Error(ex, $"Error installing package: {ex}");

            var dialog = new BetterContentDialog
            {
                Title = Resources.Label_ErrorInstallingPackage,
                Content = ex.ToString(),
                CloseButtonText = Resources.Action_Close
            };
            await dialog.ShowAsync();
        }
    }

    private Task ActuallyInstall()
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
            return Task.CompletedTask;
        }

        var setPackageInstallingStep = new SetPackageInstallingStep(settingsManager, InstallName);

        var installLocation = Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);
        var prereqStep = new SetupPrerequisitesStep(prerequisiteHelper, pyRunner);

        var downloadOptions = new DownloadPackageVersionOptions();
        var installedVersion = new InstalledPackageVersion();
        if (IsReleaseMode)
        {
            downloadOptions.VersionTag =
                SelectedVersion?.TagName
                ?? throw new NullReferenceException("Selected version is null");
            downloadOptions.IsLatest =
                AvailableVersions?.First().TagName == downloadOptions.VersionTag;

            installedVersion.InstalledReleaseVersion = downloadOptions.VersionTag;
        }
        else
        {
            downloadOptions.CommitHash =
                SelectedCommit?.Sha ?? throw new NullReferenceException("Selected commit is null");
            downloadOptions.BranchName =
                SelectedVersion?.TagName
                ?? throw new NullReferenceException("Selected version is null");
            downloadOptions.IsLatest = AvailableCommits?.First().Sha == SelectedCommit.Sha;

            installedVersion.InstalledBranch =
                SelectedVersion?.TagName
                ?? throw new NullReferenceException("Selected version is null");
            installedVersion.InstalledCommitSha = downloadOptions.CommitHash;
        }

        var downloadStep = new DownloadPackageVersionStep(
            SelectedPackage,
            installLocation,
            downloadOptions
        );
        var installStep = new InstallPackageStep(
            SelectedPackage,
            SelectedTorchVersion,
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
        return Task.CompletedTask;
    }

    public void Cancel()
    {
        OnCloseButtonClick();
    }

    private void UpdateSelectedVersionToLatestMain()
    {
        if (AvailableVersions is null)
        {
            SelectedVersion = null;
        }
        else if (SelectedPackage is FooocusMre)
        {
            SelectedVersion = AvailableVersions.FirstOrDefault(x => x.TagName == "moonride-main");
        }
        else
        {
            // First try to find master
            var version = AvailableVersions.FirstOrDefault(x => x.TagName == "master");
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
    partial void OnSelectedVersionTypeChanged(PackageVersionType value) =>
        OnSelectedPackageChanged(SelectedPackage);

    partial void OnSelectedPackageChanged(BasePackage value)
    {
        IsLoading = true;
        ReleaseNotes = string.Empty;
        AvailableVersions?.Clear();
        AvailableCommits?.Clear();

        AvailableVersionTypes = SelectedPackage.ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;
        SelectedSharedFolderMethod = SelectedPackage.RecommendedSharedFolderMethod;
        SelectedTorchVersion = SelectedPackage.GetRecommendedTorchVersion();
        if (Design.IsDesignMode)
            return;

        Dispatcher.UIThread
            .InvokeAsync(async () =>
            {
                Logger.Debug($"Release mode: {IsReleaseMode}");
                var versionOptions = await value.GetAllVersionOptions();

                AvailableVersions = IsReleaseMode
                    ? new ObservableCollection<PackageVersion>(versionOptions.AvailableVersions)
                    : new ObservableCollection<PackageVersion>(versionOptions.AvailableBranches);

                SelectedVersion = AvailableVersions.First(x => !x.IsPrerelease);
                ReleaseNotes = SelectedVersion.ReleaseNotesMarkdown;
                Logger.Debug($"Loaded release notes for {ReleaseNotes}");

                if (!IsReleaseMode)
                {
                    var commits = (await value.GetAllCommits(SelectedVersion.TagName))?.ToList();
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

    partial void OnInstallNameChanged(string? value)
    {
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(
            p => p.LibraryPath == $"Packages{Path.DirectorySeparatorChar}{value}"
        );
    }

    partial void OnSelectedVersionChanged(PackageVersion? value)
    {
        ReleaseNotes = value?.ReleaseNotesMarkdown ?? string.Empty;
        if (value == null)
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
                        Logger.Warn($"Error getting commits: {e.Message}");
                    }
                })
                .SafeFireAndForget();
        }
    }
}
