using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using StabilityMatrix.Models.Packages;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace StabilityMatrix.ViewModels;

public class InstallerViewModel : INotifyPropertyChanged
{
    private readonly ILogger<InstallerViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private string installedText;
    private int progressValue;
    private bool isIndeterminate;
    private BasePackage selectedPackage;

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

    public string InstalledText
    {
        get => installedText;
        private set
        {
            if (value == installedText) return;
            installedText = value;
            OnPropertyChanged();
        }
    }

    public int ProgressValue
    {
        get => progressValue;
        set
        {
            if (value == progressValue) return;
            progressValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressBarVisibility));
        }
    }

    public bool IsIndeterminate
    {
        get => isIndeterminate;
        set
        {
            if (value == isIndeterminate) return;
            isIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public BasePackage SelectedPackage
    {
        get => selectedPackage;
        set
        {
            selectedPackage = value;
            OnPropertyChanged();
        }
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    public AsyncRelayCommand InstallCommand => new(async () =>
    {
        var installSuccess = await InstallGitIfNecessary();
        if (!installSuccess)
        {
            logger.LogError("Git installation failed");
            return;
        }
        
        await DownloadPackage();
        
        UnzipPackage();

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
            settingsManager.AddInstalledPackage(new InstalledPackage
            {
                Name = SelectedPackage.Name,
                Path = SelectedPackage.InstallLocation,
                Id = Guid.NewGuid(),
                PackageName = SelectedPackage.Name,
                PackageVersion = "idklol"
            });
        }
    });

    
    private async Task DownloadPackage()
    {
        SelectedPackage.DownloadProgressChanged += (_, progress) =>
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
        };
        SelectedPackage.DownloadComplete += (_, _) => InstalledText = "Download Complete";
        InstalledText = "Downloading package...";
        await SelectedPackage.DownloadPackage();
    }
    
    private void UnzipPackage()
    {
        InstalledText = "Unzipping package...";
        ProgressValue = 0;
        
        Directory.CreateDirectory(SelectedPackage.InstallLocation);
        
        using var zip = ZipFile.OpenRead(SelectedPackage.DownloadLocation);
        var zipDirName = string.Empty;
        var totalEntries = zip.Entries.Count;
        var currentEntry = 0;
        
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) && entry.FullName.EndsWith("/"))
            {
                if (string.IsNullOrWhiteSpace(zipDirName))
                {
                    zipDirName = entry.FullName;
                    continue;
                }

                var folderPath = Path.Combine(SelectedPackage.InstallLocation,
                    entry.FullName.Replace(zipDirName, string.Empty));
                Directory.CreateDirectory(folderPath);
                continue;
            }

            
            var destinationPath = Path.GetFullPath(Path.Combine(SelectedPackage.InstallLocation,
                entry.FullName.Replace(zipDirName, string.Empty)));
            entry.ExtractToFile(destinationPath, true);
            currentEntry++;
            
            ProgressValue = (int) ((double) currentEntry / totalEntries * 100);
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

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
