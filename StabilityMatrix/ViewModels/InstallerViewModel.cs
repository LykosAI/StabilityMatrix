using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;
using StabilityMatrix.Python;
using StabilityMatrix.Services;
using Wpf.Ui.Controls.Window;
using Application = System.Windows.Application;
using EventManager = StabilityMatrix.Helper.EventManager;
using PackageVersion = StabilityMatrix.Models.PackageVersion;

namespace StabilityMatrix.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly ILogger<InstallerViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly IPackageFactory packageFactory;
    private readonly ISharedFolders sharedFolders;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IDownloadService downloadService;
    private readonly ISnackbarService snackbarService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;
    
    [ObservableProperty]
    private BasePackage selectedPackage;
    
    [ObservableProperty]
    private string progressText;

    [ObservableProperty] 
    private string secondaryProgressText;
    
    [ObservableProperty]
    private bool isIndeterminate;
    
    [ObservableProperty]
    private Visibility packageInstalledVisibility;
    
    [ObservableProperty]
    private string installButtonText;

    [ObservableProperty]
    private string installPath;

    [ObservableProperty] 
    private string installName;
    
    [ObservableProperty]
    private ObservableCollection<PackageVersion> availableVersions;

    [ObservableProperty] 
    private PackageVersion selectedVersion;

    [ObservableProperty] 
    private ObservableCollection<BasePackage> availablePackages;
    
    [ObservableProperty]
    private ObservableCollection<GitHubCommit> availableCommits;
    
    [ObservableProperty]
    private GitHubCommit selectedCommit;

    [ObservableProperty] 
    private string releaseNotes;

    [ObservableProperty]
    private bool isReleaseMode;

    [ObservableProperty] 
    private bool isReleaseModeEnabled;

    [ObservableProperty]
    private bool showDuplicateWarning;


    public WindowBackdropType WindowBackdropType => settingsManager.Settings.WindowBackdropType;
    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    public string ReleaseLabelText => IsReleaseMode ? "Version" : "Branch";

    internal event EventHandler? PackageInstalled;


    public InstallerViewModel(ISettingsManager settingsManager, ILogger<InstallerViewModel> logger, IPyRunner pyRunner,
        IPackageFactory packageFactory, ISnackbarService snackbarService, ISharedFolders sharedFolders,
        IPrerequisiteHelper prerequisiteHelper, IDownloadService downloadService)
    {
        this.settingsManager = settingsManager;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.packageFactory = packageFactory;
        this.sharedFolders = sharedFolders;
        this.prerequisiteHelper = prerequisiteHelper;
        this.downloadService = downloadService;
        this.snackbarService = snackbarService;

        ProgressText = "";
        SecondaryProgressText = "";
        InstallButtonText = "Install";
        ProgressValue = 0;
        InstallPath =
            $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages";
        IsReleaseMode = true;
        IsReleaseModeEnabled = true;

        AvailablePackages = new ObservableCollection<BasePackage>(packageFactory.GetAllAvailablePackages());
        if (!AvailablePackages.Any()) return;

        SelectedPackage = AvailablePackages[0];
        InstallName = SelectedPackage.DisplayName;
    }

    [RelayCommand]
    private async Task Install()
    {
        await ActuallyInstall();
        snackbarService.ShowSnackbarAsync($"Package {SelectedPackage.Name} installed successfully!",
            "Success", LogLevel.Trace);
        OnPackageInstalled();
    }

    public async Task OnLoaded()
    {
        if (SelectedPackage == null)
            return;
        
        if (SelectedPackage.ShouldIgnoreReleases)
        {
            IsReleaseMode = false;
        }

        if (IsReleaseMode)
        {
            var versions = (await SelectedPackage.GetAllVersions()).ToList();
            AvailableVersions = new ObservableCollection<PackageVersion>(versions);
            if (!AvailableVersions.Any())
                return;
            SelectedVersion = AvailableVersions[0];
        }
        else
        {
            var branches = (await SelectedPackage.GetAllBranches()).ToList();
            AvailableVersions = new ObservableCollection<PackageVersion>(branches.Select(b => new PackageVersion
            {
                TagName = b.Name,
                ReleaseNotesMarkdown = b.Commit.Label
            }));
            SelectedVersion = AvailableVersions.FirstOrDefault(x => x.TagName == "master") ?? AvailableVersions[0];
        }

        ReleaseNotes = SelectedVersion.ReleaseNotesMarkdown;
    }

    partial void OnSelectedPackageChanged(BasePackage? value)
    {
        if (value == null) return;
        
        InstallName = value.DisplayName;
        ReleaseNotes = string.Empty;
        AvailableVersions?.Clear();
        AvailableCommits?.Clear();

        // This can swallow exceptions if you don't explicitly try/catch
        // Idk how to make it better tho
        Task.Run(async () =>
        {
            if (SelectedPackage.ShouldIgnoreReleases)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsReleaseMode = false;
                    IsReleaseModeEnabled = false;
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { IsReleaseModeEnabled = true; });
            }
            
            var versions = (await value.GetAllVersions(IsReleaseMode)).ToList();
            if (!versions.Any())
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableVersions = new ObservableCollection<PackageVersion>(versions);
                SelectedVersion = AvailableVersions[0];
                ReleaseNotes = versions.First().ReleaseNotesMarkdown;
            });
            
            if (!IsReleaseMode)
            {
                var commits = await value.GetAllCommits(SelectedVersion.TagName);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableCommits = new ObservableCollection<GitHubCommit>(commits);
                    SelectedCommit = AvailableCommits[0];
                    SelectedVersion = AvailableVersions.FirstOrDefault(x => x.TagName == "master");
                });
            }
        });

    }

    partial void OnIsReleaseModeChanged(bool oldValue, bool newValue)
    {
        OnSelectedPackageChanged(SelectedPackage);
    }

    partial void OnInstallNameChanged(string oldValue, string newValue)
    {
        var path = Path.GetFullPath($"{InstallPath}\\{newValue}");
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(p => p.Path.Equals(path));
    }

    partial void OnInstallPathChanged(string oldValue, string newValue)
    {
        var path = Path.GetFullPath($"{newValue}\\{InstallName}");
        ShowDuplicateWarning = settingsManager.Settings.InstalledPackages.Any(p => p.Path.Equals(path));
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

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableCommits = new ObservableCollection<GitHubCommit>(hashes);
                        SelectedCommit = AvailableCommits[0];
                    });
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error getting commits");
                }
            });
        }
    }

    private async Task ActuallyInstall()
    {
        var isCurrentlyReleaseMode = IsReleaseMode;
        
        await InstallGitIfNecessary();

        SelectedPackage.InstallLocation = $"{InstallPath}\\{InstallName}";
        SelectedPackage.DisplayName = InstallName;

        if (!PyRunner.PipInstalled || !PyRunner.VenvInstalled)
        {
            ProgressText = "Installing dependencies...";
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

        var version = isCurrentlyReleaseMode
            ? await DownloadPackage(SelectedVersion.TagName, false)
            : await DownloadPackage(SelectedCommit.Sha, true);
        
        await InstallPackage();

        ProgressText = "Setting up shared folder links...";
        sharedFolders.SetupLinksForPackage(SelectedPackage, SelectedPackage.InstallLocation);
        
        ProgressText = "Done";
        IsIndeterminate = false;
        SelectedPackageOnProgressChanged(this, 100);

        var branch = isCurrentlyReleaseMode ? null : SelectedVersion.TagName;

        var package = new InstalledPackage
        {
            DisplayName = SelectedPackage.DisplayName,
            Path = SelectedPackage.InstallLocation,
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            PackageVersion = version,
            DisplayVersion = GetDisplayVersion(version, branch),
            InstalledBranch = branch,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        settingsManager.AddInstalledPackage(package);
        settingsManager.SetActiveInstalledPackage(package);

        ProgressValue = 0;
    }

    private string GetDisplayVersion(string version, string? branch)
    {
        return branch == null ? version : $"{branch}@{version[..7]}";
    }
    
    private Task<string?> DownloadPackage(string version, bool isCommitHash)
    {
        SelectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.DownloadComplete += (_, _) => ProgressText = "Download Complete";
        SelectedPackage.ConsoleOutput += SelectedPackageOnConsoleOutput;
        ProgressText = "Downloading package...";
        return SelectedPackage.DownloadPackage(version, isCommitHash);
    }

    private async Task InstallPackage()
    {
        SelectedPackage.InstallProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.InstallComplete += (_, _) => ProgressText = "Install Complete";
        SelectedPackage.ConsoleOutput += SelectedPackageOnConsoleOutput;
        ProgressText = "Installing package...";
        await SelectedPackage.InstallPackage();
    }

    private void SelectedPackageOnConsoleOutput(object? sender, string e)
    {
        SecondaryProgressText = e;
    }

    private async Task InstallGitIfNecessary()
    {
        void DownloadProgressHandler(object? _, ProgressReport progress)
        {
            ProgressText = $"Downloading git... ({progress.Progress}%)";
            ProgressValue = Convert.ToInt32(progress.Progress);
        }

        void DownloadFinishedHandler(object? _, ProgressReport downloadLocation)
        {
            ProgressText = "Git download complete";
            ProgressValue = 100;
        }
        
        void InstallProgressHandler(object? _, ProgressReport progress)
        {
            IsIndeterminate = progress.IsIndeterminate;
            if (progress.IsIndeterminate)
            {
                ProgressText = "Getting a few things ready...";
                ProgressValue = -1;
            }
            else
            {
                ProgressText = $"Installing git... ({Math.Ceiling(progress.Percentage)}%)";
                ProgressValue = (int) progress.Percentage;
            }
        }

        void InstallFinishedHandler(object? _, ProgressReport __)
        {
            ProgressText = "Git install complete";
            ProgressValue = 100;
        }

        prerequisiteHelper.DownloadProgressChanged += DownloadProgressHandler;
        prerequisiteHelper.DownloadComplete += DownloadFinishedHandler;
        prerequisiteHelper.InstallProgressChanged += InstallProgressHandler;
        prerequisiteHelper.InstallComplete += InstallFinishedHandler;

        await prerequisiteHelper.InstallGitIfNecessary();
        
        prerequisiteHelper.DownloadProgressChanged -= DownloadProgressHandler;
        prerequisiteHelper.DownloadComplete -= DownloadFinishedHandler;
        prerequisiteHelper.InstallProgressChanged -= InstallProgressHandler;
        prerequisiteHelper.InstallComplete -= InstallFinishedHandler;
    }
    
    private void SelectedPackageOnProgressChanged(object? sender, int progress)
    {
        if (progress == -1)
        {
            IsIndeterminate = true;
        }
        else
        {
            IsIndeterminate = false;
            ProgressValue = progress;
        }
        
        EventManager.Instance.OnGlobalProgressChanged(progress);
    }

    private void OnPackageInstalled() => PackageInstalled?.Invoke(this, EventArgs.Empty);

}
