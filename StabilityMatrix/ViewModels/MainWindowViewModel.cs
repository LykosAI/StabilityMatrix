using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using Wpf.Ui.Appearance;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<MainWindowViewModel> logger;
    private const string DefaultPackageName = "stable-diffusion-webui";

    public MainWindowViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory,
        IPrerequisiteHelper prerequisiteHelper, ILogger<MainWindowViewModel> logger)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
    }

    [ObservableProperty] 
    private bool isAdvancedMode;
    
    [ObservableProperty]
    private float progressValue;

    [ObservableProperty] 
    private string headerText;
    
    [ObservableProperty]
    private string subHeaderText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int oneClickInstallProgress;

    [ObservableProperty] 
    private bool isIndeterminate;

    [ObservableProperty]   
    private TaskbarItemProgressState progressState;

    [ObservableProperty]
    private bool showInstallButton;

    [ObservableProperty] 
    private string installButtonText;
    
    public Visibility ProgressBarVisibility => ProgressValue > 0 ? Visibility.Visible : Visibility.Collapsed;

    private string webUrl = string.Empty;

    public void OnLoaded()
    {
        SetTheme();
        ShowInstallButton = true;
        EventManager.Instance.GlobalProgressChanged += OnGlobalProgressChanged;

        if (settingsManager.Settings.InstalledPackages.Any())
        {
            IsAdvancedMode = true;
        }
        else
        {
            IsAdvancedMode = false;
            InstallButtonText = "Install";
            HeaderText = "Click the Install button below to get started!";
            SubHeaderText =
                "This will install the latest version of Stable Diffusion WebUI by Automatic1111.\nIf you don't know what this means, don't worry, you'll be generating images soon!";
        }
    }
    
    [RelayCommand]
    private void ToggleAdvancedMode()
    {
        IsAdvancedMode = !IsAdvancedMode;
        EventManager.Instance.RequestPageChange(typeof(PackageManagerPage));
    }

    [RelayCommand]
    private async Task Install()
    {
        if (InstallButtonText == "Install")
        {
            ShowInstallButton = false;
            await DoInstall();
            ShowInstallButton = true;
        }
        else
        {
            ToggleAdvancedMode();
        }
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
        SubHeaderText = "Proceeding to Launch page in 5 seconds...";
        await Task.Delay(5000);
        IsAdvancedMode = true;
    }

    private Task<string?> DownloadPackage(BasePackage selectedPackage, string version)
    {
        selectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        selectedPackage.DownloadComplete += (_, _) => SubHeaderText = "Download Complete";
        SubHeaderText = "Downloading package...";
        return selectedPackage.DownloadPackage(version: version);
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
        
        OnGlobalProgressChanged(this, progress);
    }

    /// <summary>
    ///   Updates the taskbar progress bar value and state.
    /// </summary>
    /// <param name="progress">Progress value from 0 to 100</param>
    private void OnGlobalProgressChanged(object? sender, int progress)
    {
        if (progress == -1)
        {
            ProgressState = TaskbarItemProgressState.Indeterminate;
            ProgressValue = 0;
        }
        else
        {
            ProgressState = TaskbarItemProgressState.Normal;
            ProgressValue = progress / 100f;
        }

        if (Math.Abs(ProgressValue - 1) < 0.01f)
        {
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    ProgressState = TaskbarItemProgressState.None;
                    ProgressValue = 0;
                });
            });
        }
    }

    private void SetTheme()
    {
        var theme = settingsManager.Settings.Theme;
        switch (theme)
        {
            case "Dark":
                Theme.Apply(ThemeType.Dark);
                break;
            case "Light":
                Theme.Apply(ThemeType.Light);
                break;
        }
    }
}
