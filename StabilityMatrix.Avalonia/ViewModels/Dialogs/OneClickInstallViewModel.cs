using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
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

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

public partial class OneClickInstallViewModel : ViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly ILogger<OneClickInstallViewModel> logger;
    private readonly IPyRunner pyRunner;
    private readonly ISharedFolders sharedFolders;
    private const string DefaultPackageName = "stable-diffusion-webui";
    
    [ObservableProperty] private string headerText;
    [ObservableProperty] private string subHeaderText;
    [ObservableProperty] private string subSubHeaderText = string.Empty;
    [ObservableProperty] private bool showInstallButton;
    [ObservableProperty] private bool isIndeterminate;
    [ObservableProperty] private ObservableCollection<BasePackage> allPackages;
    [ObservableProperty] private BasePackage selectedPackage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressBarVisible))]
    private int oneClickInstallProgress;

    public bool IsProgressBarVisible => OneClickInstallProgress > 0 || IsIndeterminate;

    public OneClickInstallViewModel(ISettingsManager settingsManager, IPackageFactory packageFactory,
        IPrerequisiteHelper prerequisiteHelper, ILogger<OneClickInstallViewModel> logger, IPyRunner pyRunner,
        ISharedFolders sharedFolders)
    {
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.prerequisiteHelper = prerequisiteHelper;
        this.logger = logger;
        this.pyRunner = pyRunner;
        this.sharedFolders = sharedFolders;

        HeaderText = "Welcome to Stability Matrix!";
        SubHeaderText = "Choose your preferred interface and click Install to get started!";
        ShowInstallButton = true;
        AllPackages =
            new ObservableCollection<BasePackage>(this.packageFactory.GetAllAvailablePackages());
        SelectedPackage = AllPackages[0];
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
        HeaderText = $"Installing {SelectedPackage.DisplayName}";

        var progressHandler = new Progress<ProgressReport>(progress =>
        {
            SubHeaderText = $"{progress.Title} {progress.Percentage:N0}%";
            
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

        var libraryDir = settingsManager.LibraryDir;
        
        // get latest version & download & install
        SubHeaderText = "Getting latest version...";
        var latestVersion = await SelectedPackage.GetLatestVersion();
        SelectedPackage.InstallLocation =
            Path.Combine(libraryDir, "Packages", SelectedPackage.Name);
        SelectedPackage.ConsoleOutput += (_, output) => SubSubHeaderText = output.Text;
        
        await DownloadPackage(latestVersion);
        await InstallPackage();

        SubHeaderText = "Setting up shared folder links...";
        await SelectedPackage.SetupModelFolders(SelectedPackage.InstallLocation);
        
        var installedPackage = new InstalledPackage
        {
            DisplayName = SelectedPackage.DisplayName,
            LibraryPath = Path.Combine("Packages", SelectedPackage.Name),
            Id = Guid.NewGuid(),
            PackageName = SelectedPackage.Name,
            PackageVersion = latestVersion,
            DisplayVersion = latestVersion,
            LaunchCommand = SelectedPackage.LaunchCommand,
            LastUpdateCheck = DateTimeOffset.Now
        };
        await using var st = settingsManager.BeginTransaction();
        st.Settings.InstalledPackages.Add(installedPackage);
        st.Settings.ActiveInstalledPackage = installedPackage.Id;
        EventManager.Instance.OnInstalledPackagesChanged();
        
        HeaderText = "Installation complete!";
        SubSubHeaderText = string.Empty;
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

    private async Task DownloadPackage(string version)
    {
        SubHeaderText = "Downloading package...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            OneClickInstallProgress = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(OneClickInstallProgress);
        });
        
        await SelectedPackage.DownloadPackage(version, false, progress);
        SubHeaderText = "Download Complete";
        OneClickInstallProgress = 100;
    }

    private async Task InstallPackage()
    {
        SelectedPackage.ConsoleOutput += (_, output) => SubSubHeaderText = output.Text;
        SubHeaderText = "Downloading and installing package requirements...";
        
        var progress = new Progress<ProgressReport>(progress =>
        {
            IsIndeterminate = progress.IsIndeterminate;
            OneClickInstallProgress = Convert.ToInt32(progress.Percentage);
            EventManager.Instance.OnGlobalProgressChanged(OneClickInstallProgress);
        });
        
        await SelectedPackage.InstallPackage(progress);
    }
}
