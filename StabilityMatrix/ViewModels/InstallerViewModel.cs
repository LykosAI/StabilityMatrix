using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Contracts;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private readonly ILogger<InstallerViewModel> logger;
    private readonly ISettingsManager settingsManager;

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
    private bool installButtonEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPackage))]
    private bool updateAvailable;

    public InstallerViewModel(ILogger<InstallerViewModel> logger, ISettingsManager settingsManager,
        IPackageFactory packageFactory)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;

        ProgressText = "shrug";
        InstallButtonText = "Install";
        installButtonEnabled = true;
        ProgressValue = 0;
        Packages = new ObservableCollection<BasePackage>(packageFactory.GetAllAvailablePackages());
        SelectedPackage = Packages[0];
    }

    public async Task OnLoaded()
    {
        var installedPackages = settingsManager.Settings.InstalledPackages;
        if (installedPackages.Count == 0)
        {
            return;
        }

        foreach (var packageToUpdate in installedPackages
                     .Select(package => Packages.FirstOrDefault(x => x.Name == package.Name))
                     .Where(packageToUpdate => packageToUpdate != null))
        {
            await packageToUpdate!.CheckForUpdates();
            OnSelectedPackageChanged(packageToUpdate);
        }
    }

    public ObservableCollection<BasePackage> Packages { get; }

    partial void OnSelectedPackageChanged(BasePackage value)
    {
        var installed = settingsManager.Settings.InstalledPackages;
        var isInstalled = installed.FirstOrDefault(package => package.Name == value.Name) != null;
        PackageInstalledVisibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
        UpdateAvailable = value.UpdateAvailable;
        InstallButtonText = value.UpdateAvailable ? "Update" : isInstalled ? "Launch" : "Install"; 
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task Install()
    {
        switch (InstallButtonText.ToLower())
        {
            case "install":
                await ActuallyInstall();
                break;
            case "update":
                await UpdateSelectedPackage();
                break;
            case "launch":
                EventManager.Instance.RequestPageChange(typeof(LaunchPage));
                break;
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

        var version = await DownloadPackage();
        await InstallPackage();

        ProgressText = "Installing dependencies...";
        await PyRunner.Initialize();
        if (!settingsManager.Settings.HasInstalledPip)
        {
            await PyRunner.SetupPip();
            settingsManager.SetHasInstalledPip(true);
        }

        if (!settingsManager.Settings.HasInstalledVenv)
        {
            await PyRunner.InstallPackage("virtualenv");
            settingsManager.SetHasInstalledVenv(true);
        }

        ProgressText = "Done";

        IsIndeterminate = false;
        SelectedPackageOnProgressChanged(this, 100);

        if (settingsManager.Settings.InstalledPackages.FirstOrDefault(x => x.PackageName == SelectedPackage.Name) ==
            null)
        {
            var package = new InstalledPackage
            {
                Name = SelectedPackage.Name,
                Path = SelectedPackage.InstallLocation,
                Id = Guid.NewGuid(),
                PackageName = SelectedPackage.Name,
                PackageVersion = version,
                LaunchCommand = SelectedPackage.LaunchCommand
            };
            settingsManager.AddInstalledPackage(package);
            settingsManager.SetActiveInstalledPackage(package);
        }
    }

    private async Task UpdateSelectedPackage()
    {
        ProgressText = "Updating...";
        SelectedPackageOnProgressChanged(this, 0);
        SelectedPackage.UpdateProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.UpdateComplete += (_, s) => ProgressText = s;
        
        var version = await SelectedPackage.Update();
        settingsManager.UpdatePackageVersionNumber(SelectedPackage.Name, version);
        SelectedPackage.UpdateAvailable = false;
        UpdateAvailable = false;
        InstallButtonText = "Launch";
        SelectedPackageOnProgressChanged(this, 100);
    }


    private Task<string?> DownloadPackage()
    {
        SelectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.DownloadComplete += (_, _) => ProgressText = "Download Complete";
        ProgressText = "Downloading package...";
        return SelectedPackage.DownloadPackage();
    }

    private async Task InstallPackage()
    {
        SelectedPackage.InstallProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.InstallComplete += (_, _) => ProgressText = "Install Complete";
        ProgressText = "Installing package...";
        await SelectedPackage.InstallPackage();
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
}
