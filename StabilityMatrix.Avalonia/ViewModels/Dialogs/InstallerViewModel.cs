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
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
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
    
    [ObservableProperty] private BasePackage selectedPackage;
    [ObservableProperty] private PackageVersion? selectedVersion;
    [ObservableProperty] private IReadOnlyList<BasePackage>? availablePackages;
    [ObservableProperty] private ObservableCollection<GitCommit>? availableCommits;
    [ObservableProperty] private ObservableCollection<PackageVersion>? availableVersions;
    [ObservableProperty] private GitCommit? selectedCommit;
    [ObservableProperty] private string? releaseNotes;
    [ObservableProperty] private string latestVersionText = string.Empty;
    [ObservableProperty] private bool isAdvancedMode;
    
    // Version types (release or commit)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseLabelText),
        nameof(IsReleaseMode), nameof(SelectedVersion))]
    private PackageVersionType selectedVersionType = PackageVersionType.Commit;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReleaseModeAvailable))]
    private PackageVersionType availableVersionTypes = 
        PackageVersionType.GithubRelease | PackageVersionType.Commit;
    public string ReleaseLabelText => IsReleaseMode ? "Version" : "Branch";
    public bool IsReleaseMode
    {
        get => SelectedVersionType == PackageVersionType.GithubRelease;
        set => SelectedVersionType = value ? PackageVersionType.GithubRelease : PackageVersionType.Commit;
    }

    public bool IsReleaseModeAvailable => AvailableVersionTypes.HasFlag(PackageVersionType.GithubRelease);
    
    [ObservableProperty] private bool showDuplicateWarning;
    
    [ObservableProperty] private string? installName;
    
    public Base.ProgressViewModel InstallProgress { get; } = new();

    public InstallerViewModel(
        ISettingsManager settingsManager,
        IPackageFactory packageFactory,
        IPyRunner pyRunner,
        IDownloadService downloadService, INotificationService notificationService,
        IPrerequisiteHelper prerequisiteHelper)
    {
        this.settingsManager = settingsManager;
        this.pyRunner = pyRunner;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.prerequisiteHelper = prerequisiteHelper;

        // AvailablePackages and SelectedPackage
        AvailablePackages = new ObservableCollection<BasePackage>(packageFactory.GetAllAvailablePackages());
        SelectedPackage = AvailablePackages[0];
    }

    public override void OnLoaded()
    {
        if (AvailablePackages == null) return;
        SelectedPackage = AvailablePackages[0];
        IsReleaseMode = !SelectedPackage.ShouldIgnoreReleases;
    }
    
    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode) return;
        // Check for updates
        try
        {
            var versionOptions = await SelectedPackage.GetAllVersionOptions();
            if (IsReleaseMode)
            {
                AvailableVersions =
                    new ObservableCollection<PackageVersion>(versionOptions.AvailableVersions);
                if (!AvailableVersions.Any()) return;

                SelectedVersion = AvailableVersions[0];
            }
            else
            {
                AvailableVersions =
                    new ObservableCollection<PackageVersion>(versionOptions.AvailableBranches);
                UpdateSelectedVersionToLatestMain();
            }

            ReleaseNotes = SelectedVersion?.ReleaseNotesMarkdown;
        }
        catch (Exception e)
        {
            Logger.Warn("Error getting versions: {Exception}", e.ToString());
        }
    }
    
    [RelayCommand]
    private async Task Install()
    {
        var result = await notificationService.TryAsync(ActuallyInstall(), "Could not install package");
        if (result.IsSuccessful)
        {
            notificationService.Show(new Notification(
                $"Package {SelectedPackage.Name} installed successfully!",
                "Success", NotificationType.Success));
            OnPrimaryButtonClick();  
        }
        else
        {
            var ex = result.Exception!;
            Logger.Error(ex, $"Error installing package: {ex}");

            var dialog = new BetterContentDialog
            {
                Title = "Error installing package",
                Content = ex.ToString(),
                CloseButtonText = "Close"
            };
            await dialog.ShowAsync();
        }
    }
    
    private async Task ActuallyInstall()
    {
        if (string.IsNullOrWhiteSpace(InstallName))
        {
            notificationService.Show(new Notification("Package name is empty", 
                "Please enter a name for the package", NotificationType.Error));
            return;
        }
        
        try
        {
            await InstallGitIfNecessary();
            var installLocation = Path.Combine(settingsManager.LibraryDir, "Packages", InstallName);

            if (!PyRunner.PipInstalled || !PyRunner.VenvInstalled)
            {
                InstallProgress.Text = "Installing dependencies...";
                InstallProgress.IsIndeterminate = true;
                await pyRunner.Initialize();
                
                if (!PyRunner.PipInstalled)
                {
                    await pyRunner.SetupPip();
                }
                if (!PyRunner.VenvInstalled)
                {
                    await pyRunner.InstallPackage("virtualenv");
                }
            }

            var downloadOptions = new DownloadPackageVersionOptions();
            var installedVersion = new InstalledPackageVersion();
            if (IsReleaseMode)
            {
                downloadOptions.VersionTag = SelectedVersion?.TagName ?? 
                                  throw new NullReferenceException("Selected version is null");
                installedVersion.InstalledReleaseVersion = downloadOptions.VersionTag;
            }
            else
            {
                downloadOptions.CommitHash = SelectedCommit?.Sha ?? 
                          throw new NullReferenceException("Selected commit is null");
                installedVersion.InstalledBranch = SelectedVersion?.TagName ?? 
                          throw new NullReferenceException("Selected version is null");
                installedVersion.InstalledCommitSha = downloadOptions.CommitHash;
            }
            
            await DownloadPackage(installLocation, downloadOptions);
            await InstallPackage(installLocation);

            InstallProgress.Text = "Setting up shared folder links...";
            await SelectedPackage.SetupModelFolders(installLocation);
            
            InstallProgress.Text = "Done";
            InstallProgress.IsIndeterminate = false;
            InstallProgress.Value = 100;
            EventManager.Instance.OnGlobalProgressChanged(100);

            var package = new InstalledPackage
            {
                DisplayName = InstallName,
                LibraryPath = Path.Combine("Packages", InstallName),
                Id = Guid.NewGuid(),
                PackageName = SelectedPackage.Name,
                Version = installedVersion,
                LaunchCommand = SelectedPackage.LaunchCommand,
                LastUpdateCheck = DateTimeOffset.Now
            };
            await using var st = settingsManager.BeginTransaction();
            st.Settings.InstalledPackages.Add(package);
            st.Settings.ActiveInstalledPackageId = package.Id;
        }
        finally
        {
            InstallProgress.Value = 0;
            InstallProgress.IsIndeterminate = false;
        }
    }
    
    private void UpdateSelectedVersionToLatestMain()
    {
        if (AvailableVersions is null)
        {
            SelectedVersion = null;
        }
        else
        {
            // First try to find master
            var version = AvailableVersions.FirstOrDefault(x => x.TagName == "master");
            // If not found, try main
            version ??= AvailableVersions.FirstOrDefault(x => x.TagName == "main");
        
            // If still not found, just use the first one
            version ??= AvailableVersions[0];
        
            SelectedVersion = version;
        }
    }
    
    private static string GetDisplayVersion(string version, string? branch)
    {
        return branch == null ? version : $"{branch}@{version[..7]}";
    }

    private async Task DownloadPackage(string installLocation, DownloadPackageVersionOptions downloadOptions)
    {
        InstallProgress.Text = "Downloading package...";

        var progress = new Progress<ProgressReport>(progress =>
        {
            InstallProgress.IsIndeterminate = progress.IsIndeterminate;
            InstallProgress.Value = progress.Percentage;
            EventManager.Instance.OnGlobalProgressChanged((int) progress.Percentage);
        });

        await SelectedPackage.DownloadPackage(installLocation, downloadOptions, progress);
    }

    private async Task InstallPackage(string installLocation)
    {
        InstallProgress.Text = "Installing package...";
        SelectedPackage.ConsoleOutput += SelectedPackageOnConsoleOutput;
        try
        {
            var progress = new Progress<ProgressReport>(progress =>
            {
                InstallProgress.IsIndeterminate = progress.IsIndeterminate;
                InstallProgress.Value = progress.Percentage;
                EventManager.Instance.OnGlobalProgressChanged((int) progress.Percentage);
            });
        
            await SelectedPackage.InstallPackage(installLocation, progress);
        }
        finally
        {
            SelectedPackage.ConsoleOutput -= SelectedPackageOnConsoleOutput;
        }
    }
    
    private void SelectedPackageOnConsoleOutput(object? sender, ProcessOutput e)
    {
        InstallProgress.Description = e.Text;
    }
    
    [RelayCommand]
    private async Task ShowPreview()
    {
        var url = SelectedPackage.PreviewImageUri.ToString();
        var imageStream = await downloadService.GetImageStreamFromUrl(url);
        var bitmap = new Bitmap(imageStream);
        
        var dialog = new ContentDialog
        {
            PrimaryButtonText = "Open in Browser",
            CloseButtonText = "Close",
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
    partial void OnSelectedVersionTypeChanged(PackageVersionType value) => OnSelectedPackageChanged(SelectedPackage);

    partial void OnSelectedPackageChanged(BasePackage value)
    {
        ReleaseNotes = string.Empty;
        AvailableVersions?.Clear();
        AvailableCommits?.Clear();

        AvailableVersionTypes = SelectedPackage.ShouldIgnoreReleases
            ? PackageVersionType.Commit
            : PackageVersionType.GithubRelease | PackageVersionType.Commit;

        if (Design.IsDesignMode) return;
        
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            Logger.Debug($"Release mode: {IsReleaseMode}");
            var versionOptions = await value.GetAllVersionOptions();
            
            AvailableVersions = IsReleaseMode
                ? new ObservableCollection<PackageVersion>(versionOptions.AvailableVersions)
                : new ObservableCollection<PackageVersion>(versionOptions.AvailableBranches);

            SelectedVersion = AvailableVersions[0];
            ReleaseNotes = SelectedVersion.ReleaseNotesMarkdown;
            
            Logger.Debug($"Loaded release notes for {ReleaseNotes}");
            
            if (!IsReleaseMode)
            {
                var commits = (await value.GetAllCommits(SelectedVersion.TagName))?.ToList();
                if (commits is null || commits.Count == 0) return;
                
                AvailableCommits = new ObservableCollection<GitCommit>(commits);
                SelectedCommit = AvailableCommits[0];
                UpdateSelectedVersionToLatestMain();
            }

            InstallName = SelectedPackage.DisplayName;
            LatestVersionText = IsReleaseMode
                ? $"Latest version: {SelectedVersion.TagName}"
                : $"Branch: {SelectedVersion.TagName}";
        }).SafeFireAndForget();
    }
    
    private async Task InstallGitIfNecessary()
    {
        var progressHandler = new Progress<ProgressReport>(progress =>
        {
            if (progress.Message != null && progress.Message.Contains("Downloading"))
            {
                InstallProgress.Text = $"Downloading prerequisites... {progress.Percentage:N0}%";
            }
            else if (progress.Type == ProgressType.Extract)
            {
                InstallProgress.Text = $"Installing git... {progress.Percentage:N0}%";
            }
            else if (progress.Title != null && progress.Title.Contains("Unpacking"))
            {
                InstallProgress.Text = $"Unpacking resources... {progress.Percentage:N0}%";
            }
            else
            {
                InstallProgress.Text = progress.Message;
            }

            InstallProgress.IsIndeterminate = progress.IsIndeterminate;
            InstallProgress.Value = Convert.ToInt32(progress.Percentage);
        });

        await prerequisiteHelper.InstallAllIfNecessary(progressHandler);
    }
    
    partial void OnInstallNameChanged(string? value)
    {
        ShowDuplicateWarning =
            settingsManager.Settings.InstalledPackages.Any(p =>
                p.LibraryPath == $"Packages{Path.DirectorySeparatorChar}{value}");
    }
    
    partial void OnSelectedVersionChanged(PackageVersion? value)
    {
        ReleaseNotes = value?.ReleaseNotesMarkdown ?? string.Empty;
        if (value == null) return;
        
        SelectedCommit = null;
        AvailableCommits?.Clear();
        
        if (!IsReleaseMode)
        {
            Task.Run(async () =>
            {
                try
                {
                    var hashes = await SelectedPackage.GetAllCommits(value.TagName);
                    if (hashes is null) throw new Exception("No commits found");
                    
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
            }).SafeFireAndForget();
        }
    }
}
