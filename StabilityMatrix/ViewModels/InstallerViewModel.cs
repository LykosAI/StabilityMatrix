using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using StabilityMatrix.Models.Packages;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

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
    private string installedText;
    [ObservableProperty]
    private bool isIndeterminate;
    [ObservableProperty]
    private Visibility packageInstalledVisibility;

    public InstallerViewModel(ILogger<InstallerViewModel> logger, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        InstalledText = "shrug";
        ProgressValue = 0;
        Packages = new ObservableCollection<BasePackage>
        {
            new A3WebUI(),
            new DankDiffusion()
        };
        SelectedPackage = Packages[0];
    }

    public Task OnLoaded()
    {
        return Task.CompletedTask;
    }

    public ObservableCollection<BasePackage> Packages { get; }

    partial void OnSelectedPackageChanged(BasePackage value)
    {
        var installed = settingsManager.Settings.InstalledPackages;
        PackageInstalledVisibility = installed.FirstOrDefault(package => package.Name == value.Name) != null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task Install()
    {
        var installSuccess = await InstallGitIfNecessary();
        if (!installSuccess)
        {
            logger.LogError("Git installation failed");
            return;
        }

        await DownloadPackage();
        await InstallPackage();

        InstalledText = "Installing dependencies...";
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

        InstalledText = "Done";

        IsIndeterminate = false;
        ProgressValue = 100;

        if (settingsManager.Settings.InstalledPackages.FirstOrDefault(x => x.PackageName == SelectedPackage.Name) ==
            null)
        {
            var package = new InstalledPackage
            {
                Name = SelectedPackage.Name,
                Path = SelectedPackage.InstallLocation,
                Id = Guid.NewGuid(),
                PackageName = SelectedPackage.Name,
                PackageVersion = "idklol",
                LaunchCommand = SelectedPackage.LaunchCommand
            };
            settingsManager.AddInstalledPackage(package);
            settingsManager.SetActiveInstalledPackage(package);
        }
    }


    private async Task DownloadPackage()
    {
        SelectedPackage.DownloadProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.DownloadComplete += (_, _) => InstalledText = "Download Complete";
        InstalledText = "Downloading package...";
        await SelectedPackage.DownloadPackage();
    }

    private async Task InstallPackage()
    {
        SelectedPackage.InstallProgressChanged += SelectedPackageOnProgressChanged;
        SelectedPackage.InstallComplete += (_, _) => InstalledText = "Install Complete";
        InstalledText = "Installing package...";
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
    }

    private async Task<bool> InstallGitIfNecessary()
    {
        var gitOutput = await ProcessRunner.GetProcessOutputAsync("git", "--version");
        if (gitOutput.Contains("git version 2"))
        {
            return true;
        }

        IsIndeterminate = true;
        InstalledText = "Installing Git...";
        using var installProcess = ProcessRunner.StartProcess("Assets\\Git-2.40.1-64-bit.exe", "/VERYSILENT /NORESTART");
        installProcess.OutputDataReceived += (sender, args) =>
        {
            Debug.Write(args.Data);
        };
        await installProcess.WaitForExitAsync();
        IsIndeterminate = false;

        return installProcess.ExitCode == 0;
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
