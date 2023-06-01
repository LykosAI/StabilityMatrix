using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Api;
using StabilityMatrix.Python;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly ILogger<InstallerViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly IPackageFactory packageFactory;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;
    
    [ObservableProperty]
    private BasePackage selectedPackage;
    
    [ObservableProperty]
    private string progressText;
    
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
    private ObservableCollection<GithubCommit> availableCommits;
    
    [ObservableProperty]
    private GithubCommit selectedCommit;

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
        IPackageFactory packageFactory)
    {
        this.settingsManager = settingsManager;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.packageFactory = packageFactory;
        
        ProgressText = "";
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
        OnPackageInstalled();
    }

    public async Task OnLoaded()
    {
        if (SelectedPackage == null)
            return;

        var releases = (await SelectedPackage.GetAllReleases()).ToList();
        if (!releases.Any())
        {
            IsReleaseMode = false;
        }

        var versions = (await SelectedPackage.GetAllVersions()).ToList();
        AvailableVersions = new ObservableCollection<PackageVersion>(versions);
        if (!AvailableVersions.Any())
            return;

        SelectedVersion = AvailableVersions[0];
        ReleaseNotes = versions.First().ReleaseNotesMarkdown;
    }

    partial void OnSelectedPackageChanged(BasePackage? value)
    {
        if (value == null) return;
        
        InstallName = value.DisplayName;
        ReleaseNotes = string.Empty;
        AvailableVersions?.Clear();

        // This can swallow exceptions if you don't explicitly try/catch
        // Idk how to make it better tho
        Task.Run(async () =>
        {
            var releases = (await SelectedPackage.GetAllReleases()).ToList();
            if (!releases.Any())
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
                    AvailableCommits = new ObservableCollection<GithubCommit>(commits);
                    SelectedCommit = AvailableCommits[0];
                    SelectedVersion = AvailableVersions.First(x => x.TagName == "master");
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
                    AvailableCommits = new ObservableCollection<GithubCommit>(hashes);
                    await Task.Delay(10); // or it doesn't work sometimes? lolwut?
                    SelectedCommit = AvailableCommits[0];
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
        var installSuccess = await InstallGitIfNecessary();
        if (!installSuccess)
        {
            logger.LogError("Git installation failed");
            return;
        }

        SelectedPackage.InstallLocation = $"{InstallPath}\\{InstallName}";
        SelectedPackage.DisplayName = InstallName;

        var version = await DownloadPackage(SelectedVersion.TagName);
        await InstallPackage();

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

        ProgressText = "Done";

        IsIndeterminate = false;
        SelectedPackageOnProgressChanged(this, 100);

        var package = new InstalledPackage
        {
            DisplayName = SelectedPackage.DisplayName,
            Path = SelectedPackage.InstallLocation,
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            PackageVersion = version,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        settingsManager.AddInstalledPackage(package);
        settingsManager.SetActiveInstalledPackage(package);

        ProgressValue = 0;
    }
    
    private Task<string?> DownloadPackage(string version)
    {
        SelectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.DownloadComplete += (_, _) => ProgressText = "Download Complete";
        ProgressText = "Downloading package...";
        return SelectedPackage.DownloadPackage(version: version);
    }

    private async Task InstallPackage()
    {
        SelectedPackage.InstallProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.InstallComplete += (_, _) => ProgressText = "Install Complete";
        ProgressText = "Installing package...";
        await SelectedPackage.InstallPackage();
    }
    
    private async Task<bool> InstallGitIfNecessary()
    {
        try
        {
            var gitOutput = await ProcessRunner.GetProcessOutputAsync("git", "--version");
            if (gitOutput.Contains("git version 2"))
            {
                return true;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error running git: ");
        }

        IsIndeterminate = true;
        ProgressText = "Installing Git...";
        using var installProcess =
            ProcessRunner.StartProcess("Assets\\Git-2.40.1-64-bit.exe", "/VERYSILENT /NORESTART");
        installProcess.OutputDataReceived += (sender, args) => { Debug.Write(args.Data); };
        await installProcess.WaitForExitAsync();
        IsIndeterminate = false;

        return installProcess.ExitCode == 0;
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
