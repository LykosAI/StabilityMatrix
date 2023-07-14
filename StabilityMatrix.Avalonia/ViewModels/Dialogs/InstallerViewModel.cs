using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Octokit;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Notification = Avalonia.Controls.Notifications.Notification;
using PackageVersion = StabilityMatrix.Core.Models.PackageVersion;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;


public partial class InstallerViewModel : ContentDialogViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly ISettingsManager settingsManager;
    private readonly IPyRunner pyRunner;
    private readonly IDownloadService downloadService;
    private readonly INotificationService notificationService;
    private readonly ISharedFolders sharedFolders;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    
    [ObservableProperty] private BasePackage selectedPackage;
    [ObservableProperty] private PackageVersion? selectedVersion;

    [ObservableProperty] private IReadOnlyList<BasePackage>? availablePackages;
    [ObservableProperty] private ObservableCollection<GitHubCommit>? availableCommits;
    [ObservableProperty] private ObservableCollection<PackageVersion>? availableVersions;
    
    [ObservableProperty] private GitHubCommit? selectedCommit;

    [ObservableProperty] private string? releaseNotes;
    // [ObservableProperty] private bool isReleaseMode;
    
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
    
    internal event EventHandler? PackageInstalled;
    
    public ProgressViewModel InstallProgress { get; } = new()
    {

    };

    public InstallerViewModel(
        ISettingsManager settingsManager,
        IPyRunner pyRunner,
        IDownloadService downloadService, INotificationService notificationService,
        ISharedFolders sharedFolders,
        IPrerequisiteHelper prerequisiteHelper)
    {
        this.settingsManager = settingsManager;
        this.pyRunner = pyRunner;
        this.downloadService = downloadService;
        this.notificationService = notificationService;
        this.sharedFolders = sharedFolders;
        this.prerequisiteHelper = prerequisiteHelper;

        // AvailablePackages and SelectedPackage need to be set in init
    }

    public override void OnLoaded()
    {
        if (AvailablePackages == null) return;
        SelectedPackage = AvailablePackages[0];
    }
    
    public override async Task OnLoadedAsync()
    {
        if (IsReleaseMode)
        {
            var versions = (await SelectedPackage.GetAllVersions()).ToList();
            AvailableVersions = new ObservableCollection<PackageVersion>(versions);
            if (!AvailableVersions.Any()) return;
            
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
            SelectedVersion = AvailableVersions.FirstOrDefault(x => 
                x.TagName.ToLowerInvariant() is "master" or "main") ?? AvailableVersions[0];
        }

        ReleaseNotes = SelectedVersion.ReleaseNotesMarkdown;
    }
    
    [RelayCommand]
    private async Task Install()
    {
        await ActuallyInstall();
        notificationService.Show(new Notification(
            $"Package {SelectedPackage.Name} installed successfully!",
            "Success", NotificationType.Success));
        OnPackageInstalled();
    }
    
    private async Task ActuallyInstall()
    {
        if (string.IsNullOrWhiteSpace(InstallName))
        {
            notificationService.Show(new Notification("Package name is empty", 
                "Please enter a name for the package", NotificationType.Error));
        }
        
        await InstallGitIfNecessary();

        var libraryDir = new DirectoryPath(settingsManager.LibraryDir, "Packages", InstallName);
        SelectedPackage.InstallLocation = $"{settingsManager.LibraryDir}\\Packages\\{InstallName}";
        // SelectedPackage.DisplayName = InstallName;

        if (!PyRunner.PipInstalled || !PyRunner.VenvInstalled)
        {
            InstallProgress.Text = "Installing dependencies...";
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

        var version = IsReleaseMode
            ? await DownloadPackage(SelectedVersion!.TagName, false)
            : await DownloadPackage(SelectedCommit!.Sha, true);
        
        await InstallPackage();

        InstallProgress.Text = "Setting up shared folder links...";
        sharedFolders.SetupLinksForPackage(SelectedPackage, SelectedPackage.InstallLocation);
        
        InstallProgress.Text = "Done";
        InstallProgress.IsIndeterminate = false;
        InstallProgress.Value = 100;
        EventManager.Instance.OnGlobalProgressChanged(100);

        var branch = SelectedVersionType == PackageVersionType.GithubRelease ? 
            null : SelectedVersion!.TagName;

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
        st.Settings.ActiveInstalledPackage = package.Id;
        
        InstallProgress.Value = 0;
    }
    
    private static string GetDisplayVersion(string version, string? branch)
    {
        return branch == null ? version : $"{branch}@{version[..7]}";
    }
    
    private Task<string?> DownloadPackage(string version, bool isCommitHash)
    {
        InstallProgress.Text = "Downloading package...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            InstallProgress.IsIndeterminate = progress.IsIndeterminate;
            InstallProgress.Value = progress.Percentage;
            EventManager.Instance.OnGlobalProgressChanged((int) progress.Percentage);
        });
        
        return SelectedPackage.DownloadPackage(version, isCommitHash, progress);
    }

    private async Task InstallPackage()
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
        
            await SelectedPackage.InstallPackage(progress);
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
            Title = "Test title",
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

    partial void OnSelectedPackageChanged(BasePackage? value)
    {
        if (value == null) return;
        
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
            var versions = (await value.GetAllVersions(IsReleaseMode)).ToList();
            
            if (!versions.Any()) return;

            AvailableVersions = new ObservableCollection<PackageVersion>(versions);
            Logger.Debug($"Available versions: {string.Join(", ", AvailableVersions)}");
            SelectedVersion = AvailableVersions[0];
            ReleaseNotes = versions.First().ReleaseNotesMarkdown;
            Logger.Debug($"Loaded release notes for {ReleaseNotes}");
            
            if (!IsReleaseMode)
            {
                var commits = await value.GetAllCommits(SelectedVersion.TagName);
                if (commits is null || commits.Count == 0) return;
                
                AvailableCommits = new ObservableCollection<GitHubCommit>(commits);
                SelectedCommit = AvailableCommits[0];
                SelectedVersion = AvailableVersions?.FirstOrDefault(packageVersion => 
                    packageVersion.TagName.ToLowerInvariant() is "master" or "main");
            }
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
                        AvailableCommits = new ObservableCollection<GitHubCommit>(hashes);
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
    
    private void OnPackageInstalled() => PackageInstalled?.Invoke(this, EventArgs.Empty);
}
