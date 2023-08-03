using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using EventManager = StabilityMatrix.Core.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class OneClickInstallViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly ISharedFolders sharedFolders;
    private const string DefaultPackageName = "stable-diffusion-webui";
    
    [ObservableProperty] private string headerText;
    [ObservableProperty] private string subHeaderText;
    [ObservableProperty] private string subSubHeaderText;
    [ObservableProperty] private bool showInstallButton;
    [ObservableProperty] private bool isIndeterminate;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int oneClickInstallProgress;


    public Visibility ProgressBarVisibility =>
        OneClickInstallProgress > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    public OneClickInstallViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory,
        IPrerequisiteHelper prerequisiteHelper, ILogger<MainWindowViewModel> logger, IPyRunner pyRunner,
        ISharedFolders sharedFolders)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.sharedFolders = sharedFolders;

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
        EventManager.Instance.OnOneClickInstallFinished(true);
        return Task.CompletedTask;
    }
    
    private async Task DoInstall()
    {
        var a1111 = packageFactory.FindPackageByName(DefaultPackageName)!;
        HeaderText = "Installing Stable Diffusion WebUI";

        var progressHandler = new Progress<ProgressReport>(progress =>
        {
            if (progress.Message != null && progress.Message.Contains("Downloading"))
            {
                SubHeaderText = $"Downloading prerequisites... {progress.Percentage:N0}%";
            }
            else if (progress.Type == ProgressType.Extract)
            {
                SubHeaderText = $"Installing git... {progress.Percentage:N0}%";
            }
            else if (progress.Title != null && progress.Title.Contains("Unpacking"))
            {
                SubHeaderText = $"Unpacking resources... {progress.Percentage:N0}%";
            }
            else if (progress.Message != null)
            {
                SubHeaderText = progress.Message;
            }
            
            IsIndeterminate = progress.IsIndeterminate;
            OneClickInstallProgress = Convert.ToInt32(progress.Percentage);
        });
        
        await prerequisiteHelper.InstallAllIfNecessary(progressHandler);

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
        a1111.InstallLocation = $"{settingsManager.LibraryDir}\\Packages\\stable-diffusion-webui";
        a1111.ConsoleOutput += (_, output) => SubSubHeaderText = output.Text;
        
        await DownloadPackage(a1111, latestVersion);
        await InstallPackage(a1111);

        SubHeaderText = "Setting up shared folder links...";
        sharedFolders.SetupLinksForPackage(a1111, a1111.InstallLocation);
        
        var package = new InstalledPackage
        {
            DisplayName = a1111.DisplayName,
            LibraryPath = "Packages\\stable-diffusion-webui",
            Id = Guid.NewGuid(),
            PackageName = a1111.Name,
            PackageVersion = latestVersion,
            DisplayVersion = latestVersion,
            LaunchCommand = a1111.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        await using var st = settingsManager.BeginTransaction();
        st.Settings.InstalledPackages.Add(package);
        st.Settings.ActiveInstalledPackageId = package.Id;
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
        EventManager.Instance.OnOneClickInstallFinished(false);
    }

    private async Task DownloadPackage(BasePackage selectedPackage, string version)
    {
        SubHeaderText = "Downloading package...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            OneClickInstallProgress = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(OneClickInstallProgress);
        });
        
        await selectedPackage.DownloadPackage(version, false, progress);
        SubHeaderText = "Download Complete";
        OneClickInstallProgress = 100;
    }

    private async Task InstallPackage(BasePackage selectedPackage)
    {
        selectedPackage.ConsoleOutput += (_, output) => SubSubHeaderText = output.Text;
        SubHeaderText = "Downloading and installing package requirements...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            OneClickInstallProgress = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(OneClickInstallProgress);
        });
        
        await selectedPackage.InstallPackage(progress);
    }
}
