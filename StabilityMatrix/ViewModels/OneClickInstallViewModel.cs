using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Packages;
using StabilityMatrix.Python;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class OneClickInstallViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IPyRunner pyRunner;
    private const string DefaultPackageName = "stable-diffusion-webui";
    
    [ObservableProperty] private string headerText;
    [ObservableProperty] private string subHeaderText;
    [ObservableProperty] private bool showInstallButton;
    [ObservableProperty] private bool isIndeterminate;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int oneClickInstallProgress;

    
    public Visibility ProgressBarVisibility => OneClickInstallProgress > 0 ? Visibility.Visible : Visibility.Collapsed;

    public OneClickInstallViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory,
        IPrerequisiteHelper prerequisiteHelper, ILogger<MainWindowViewModel> logger, IPyRunner pyRunner)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        
        HeaderText = "Welcome to Stability Matrix!";
        SubHeaderText =
            "Click the Install button below to get started!\n" +
            "This will install the latest version of Stable Diffusion WebUI by Automatic1111.\n" +
            "If you don't know what this means, don't worry, you'll be generating images soon!";
        ShowInstallButton = true;
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
        EventManager.Instance.OnOneClickInstallFinished();
        return Task.CompletedTask;
    }
    
    private async Task DoInstall()
    {
        var a1111 = packageFactory.FindPackageByName(DefaultPackageName)!;
        HeaderText = "Installing Stable Diffusion WebUI...";
        // check git
        var gitProcess = await prerequisiteHelper.InstallGitIfNecessary();
        if (gitProcess != null) // git isn't installed
        {
            IsIndeterminate = true;
            SubHeaderText = "Installing git...";
            await gitProcess.WaitForExitAsync();
            if (gitProcess.ExitCode != 0)
            {
                HeaderText = "Installation failed";
                SubHeaderText = "Error installing git. Please try again later.";
                OneClickInstallProgress = 0;
                logger.LogError($"Git install failed with exit code {gitProcess.ExitCode}");
            }
        }

        SubHeaderText = "Installing prerequisites...";
        IsIndeterminate = true;
        if (!PyRunner.PipInstalled)
        {
            await pyRunner.SetupPip();
        }

        if (!PyRunner.VenvInstalled)
        {
            await pyRunner.InstallPackage("virtualenv");
        }
        IsIndeterminate = false;

        // get latest version & download & install
        SubHeaderText = "Getting latest version...";
        var latestVersion = await a1111.GetLatestVersion();
        a1111.InstallLocation += "\\stable-diffusion-webui";
        await DownloadPackage(a1111, latestVersion);
        await InstallPackage(a1111);
        
        var package = new InstalledPackage
        {
            DisplayName = a1111.DisplayName,
            Path = a1111.InstallLocation,
            Id = Guid.NewGuid(),
            PackageName = a1111.Name,
            PackageVersion = latestVersion,
            LaunchCommand = a1111.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        settingsManager.AddInstalledPackage(package);
        settingsManager.SetActiveInstalledPackage(package);
        EventManager.Instance.OnInstalledPackagesChanged();
        
        HeaderText = "Installation complete!";
        OneClickInstallProgress = 100;
        SubHeaderText = "Proceeding to Launch page in 3 seconds...";
        await Task.Delay(1000);
        SubHeaderText = "Proceeding to Launch page in 2 seconds...";
        await Task.Delay(1000);
        SubHeaderText = "Proceeding to Launch page in 1 second...";
        await Task.Delay(1000);
        
        // should close dialog
        EventManager.Instance.OnOneClickInstallFinished();
    }

    private Task<string?> DownloadPackage(BasePackage selectedPackage, string version)
    {
        selectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        selectedPackage.DownloadComplete += (_, _) => SubHeaderText = "Download Complete";
        SubHeaderText = "Downloading package...";
        return selectedPackage.DownloadPackage(version, false);
    }

    private async Task InstallPackage(BasePackage selectedPackage)
    {
        selectedPackage.InstallProgressChanged += SelectedPackageOnProgressChanged;
        selectedPackage.InstallComplete += (_, _) => HeaderText = "Install Complete";
        SubHeaderText = "Installing package...";
        await selectedPackage.InstallPackage();
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
            OneClickInstallProgress = progress;
        }
        
        EventManager.Instance.OnGlobalProgressChanged(progress);
    }
}
