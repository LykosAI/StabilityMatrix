using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Octokit;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Services;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.ContentDialogControl;
using Application = System.Windows.Application;
using EventManager = StabilityMatrix.Core.Helper.EventManager;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;
using PackageVersion = StabilityMatrix.Core.Models.PackageVersion;

namespace StabilityMatrix.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly ILogger<InstallerViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly ISharedFolders sharedFolders;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IContentDialogService contentDialogService;
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


    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    public string ReleaseLabelText => IsReleaseMode ? "Version" : "Branch";

    internal event EventHandler? PackageInstalled;


    public InstallerViewModel(ISettingsManager settingsManager, ILogger<InstallerViewModel> logger, IPyRunner pyRunner,
        IPackageFactory packageFactory, ISnackbarService snackbarService, ISharedFolders sharedFolders,
        IPrerequisiteHelper prerequisiteHelper, InstallerWindowDialogService contentDialogService)
    {
        this.settingsManager = settingsManager;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.sharedFolders = sharedFolders;
        this.prerequisiteHelper = prerequisiteHelper;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;

        ProgressText = "";
        SecondaryProgressText = "";
        InstallButtonText = "Install";
        ProgressValue = 0;
        IsReleaseMode = true;
        IsReleaseModeEnabled = true;

        AvailablePackages = new ObservableCollection<BasePackage>(packageFactory.GetAllAvailablePackages());
        if (!AvailablePackages.Any()) return;

        SelectedPackage = AvailablePackages[0];
        InstallName = SelectedPackage.DisplayName;
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
        
        ShowDuplicateWarning =
            settingsManager.Settings.InstalledPackages.Any(p =>
                p.LibraryPath.Equals($"Packages\\{InstallName}"));
    }
    
    [RelayCommand]
    private async Task Install()
    {
        await ActuallyInstall();
        snackbarService.ShowSnackbarAsync($"Package {SelectedPackage.Name} installed successfully!",
            "Success", ControlAppearance.Success).SafeFireAndForget();
        OnPackageInstalled();
    }

    [RelayCommand]
    private async Task ShowPreview()
    {
        var bitmap = new BitmapImage(SelectedPackage.PreviewImageUri);
        var dialog = contentDialogService.CreateDialog();
        dialog.Content = new Image
        {
            Source = bitmap, 
            Stretch = Stretch.Uniform, 
            MaxHeight = 500,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        dialog.PrimaryButtonText = "Open in Browser";
        
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedPackage.PreviewImageUri.ToString(),
                UseShellExecute = true
            });
        }
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

    partial void OnInstallNameChanged(string? oldValue, string newValue)
    {
        ShowDuplicateWarning =
            settingsManager.Settings.InstalledPackages.Any(p =>
                p.LibraryPath.Equals($"Packages\\{newValue}"));
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

        SelectedPackage.InstallLocation = $"{settingsManager.LibraryDir}\\Packages\\{InstallName}";
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
        ProgressValue = 100;
        EventManager.Instance.OnGlobalProgressChanged(100);

        var branch = isCurrentlyReleaseMode ? null : SelectedVersion.TagName;

        var package = new InstalledPackage
        {
            DisplayName = SelectedPackage.DisplayName,
            LibraryPath = $"Packages\\{InstallName}",
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            PackageVersion = version,
            DisplayVersion = GetDisplayVersion(version, branch),
            InstalledBranch = branch,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        await using var st = settingsManager.BeginTransaction();
        st.Settings.InstalledPackages.Add(package);
        st.Settings.ActiveInstalledPackageId = package.Id;
        
        ProgressValue = 0;
    }

    private string GetDisplayVersion(string version, string? branch)
    {
        return branch == null ? version : $"{branch}@{version[..7]}";
    }
    
    private Task<string?> DownloadPackage(string version, bool isCommitHash)
    {
        ProgressText = "Downloading package...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            ProgressValue = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(ProgressValue);
        });
        
        return SelectedPackage.DownloadPackage(version, isCommitHash, progress);
    }

    private async Task InstallPackage()
    {
        SelectedPackage.ConsoleOutput += SelectedPackageOnConsoleOutput;
        ProgressText = "Installing package...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            ProgressValue = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(ProgressValue);
        });
        
        await SelectedPackage.InstallPackage(progress);
    }

    private void SelectedPackageOnConsoleOutput(object? sender, ProcessOutput e)
    {
        SecondaryProgressText = e.Text;
    }

    private async Task InstallGitIfNecessary()
    {
        var progressHandler = new Progress<ProgressReport>(progress =>
        {
            if (progress.Message != null && progress.Message.Contains("Downloading"))
            {
                ProgressText = $"Downloading prerequisites... {progress.Percentage:N0}%";
            }
            else if (progress.Type == ProgressType.Extract)
            {
                ProgressText = $"Installing git... {progress.Percentage:N0}%";
            }
            else if (progress.Title != null && progress.Title.Contains("Unpacking"))
            {
                ProgressText = $"Unpacking resources... {progress.Percentage:N0}%";
            }
            else
            {
                ProgressText = progress.Message;
            }

            IsIndeterminate = progress.IsIndeterminate;
            ProgressValue = Convert.ToInt32(progress.Percentage);
        });

        await prerequisiteHelper.InstallAllIfNecessary(progressHandler);
    }

    private void OnPackageInstalled() => PackageInstalled?.Invoke(this, EventArgs.Empty);

}
