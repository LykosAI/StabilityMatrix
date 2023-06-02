using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Polly;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.ContentDialogControl;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class PackageManagerViewModel : ObservableObject
{
    private readonly ILogger<PackageManagerViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IDialogFactory dialogFactory;
    private readonly IContentDialogService contentDialogService;
    private readonly IDialogErrorHandler dialogErrorHandler;
    private const int MinutesToWaitForUpdateCheck = 60;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;
    
    [ObservableProperty]
    private InstalledPackage selectedPackage;
    
    [ObservableProperty]
    private string progressText;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty]
    private string installButtonText;

    [ObservableProperty] 
    private bool installButtonEnabled;

    [ObservableProperty] 
    private Visibility installButtonVisibility;

    [ObservableProperty] 
    private bool isUninstalling;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPackage))]
    private bool updateAvailable;

    public PackageManagerViewModel(ILogger<PackageManagerViewModel> logger, ISettingsManager settingsManager,
        IPackageFactory packageFactory, IDialogFactory dialogFactory, IContentDialogService contentDialogService, IDialogErrorHandler dialogErrorHandler)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.dialogFactory = dialogFactory;
        this.contentDialogService = contentDialogService;
        this.dialogErrorHandler = dialogErrorHandler;

        ProgressText = "shrug";
        InstallButtonText = "Install";
        installButtonEnabled = true;
        ProgressValue = 0;
        Packages = new ObservableCollection<InstalledPackage>(settingsManager.Settings.InstalledPackages);

        if (Packages.Any())
        {
            SelectedPackage = Packages[0];
            InstallButtonVisibility = Visibility.Visible;
        }
        else
        {
            SelectedPackage = new InstalledPackage
            {
                DisplayName = "Click \"Add Package\" to install a package"
            };
            InstallButtonVisibility = Visibility.Collapsed;
        }
    }

    public async Task OnLoaded()
    {
        Packages.Clear();
        var installedPackages = settingsManager.Settings.InstalledPackages;
        if (installedPackages.Count == 0)
        {
            SelectedPackage = new InstalledPackage
            {
                DisplayName = "Click \"Add Package\" to install a package"
            };
            InstallButtonVisibility = Visibility.Collapsed;
            
            return;
        }
        
        
        foreach (var packageToUpdate in installedPackages)
        {
            var basePackage = packageFactory.FindPackageByName(packageToUpdate.PackageName);
            if (basePackage == null) continue;
            
            var canCheckUpdate = packageToUpdate.LastUpdateCheck == null ||
                            packageToUpdate.LastUpdateCheck.Value.AddMinutes(MinutesToWaitForUpdateCheck) <
                            DateTimeOffset.Now;
            if (canCheckUpdate)
            {
                var hasUpdate = await basePackage.CheckForUpdates(packageToUpdate.DisplayName);
                packageToUpdate.UpdateAvailable = hasUpdate;
                packageToUpdate.LastUpdateCheck = DateTimeOffset.Now;
                settingsManager.SetLastUpdateCheck(packageToUpdate);
            }

            Packages.Add(packageToUpdate);
        }

        SelectedPackage =
            installedPackages.FirstOrDefault(x => x.Id == settingsManager.Settings.ActiveInstalledPackage) ??
            Packages[0];
    }

    public ObservableCollection<InstalledPackage> Packages { get; }

    partial void OnSelectedPackageChanged(InstalledPackage? value)
    {
        if (value == null) return;
        
        UpdateAvailable = value.UpdateAvailable;
        InstallButtonText = value.UpdateAvailable ? "Update" : "Launch";
        InstallButtonVisibility = Visibility.Visible;
    }

    public Visibility ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private async Task Install()
    {
        switch (InstallButtonText.ToLower())
        {
            case "update":
                await UpdateSelectedPackage();
                break;
            case "launch":
                EventManager.Instance.RequestPageChange(typeof(LaunchPage));
                break;
        }
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        if (SelectedPackage?.Path == null)
        {
            logger.LogError("No package selected to uninstall");
            return;
        }
        
        var dialog = contentDialogService.CreateDialog();
        dialog.Title = "Are you sure?";
        dialog.Content = "This will delete all folders in the package directory, including any generated images in that directory as well as any files you may have added.";
        dialog.PrimaryButtonText = "Yes, delete it";
        dialog.CloseButtonText = "No, keep it";
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            IsUninstalling = true;
            var deleteTask = DeleteDirectoryAsync(SelectedPackage.Path);
            var taskResult = await dialogErrorHandler.TryAsync(deleteTask,
                "Some files could not be deleted. Please close any open files in the package directory and try again.");
            if (taskResult.IsSuccessful)
            {
                settingsManager.RemoveInstalledPackage(SelectedPackage);
            }
            await OnLoaded();
            IsUninstalling = false;
        }
    }
    
    /// <summary>
    /// Deletes a directory and all of its contents recursively.
    /// Uses Polly to retry the deletion if it fails, up to 5 times with an exponential backoff.
    /// </summary>
    /// <param name="targetDirectory"></param>
    private Task DeleteDirectoryAsync(string targetDirectory)
    {
        var policy = Policy.Handle<IOException>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)),
                onRetry: (exception, calculatedWaitDuration) =>
                {
                    logger.LogWarning(
                        exception, 
                        "Deletion of {TargetDirectory} failed. Retrying in {CalculatedWaitDuration}", 
                        targetDirectory, calculatedWaitDuration);
                });

        return policy.ExecuteAsync(async () =>
        {
            await Task.Run(() =>
            {
                DeleteDirectory(targetDirectory);
            });
        });
    }
    
    private void DeleteDirectory(string targetDirectory)
    {
        // Delete all files in the directory
        var fileEntries = Directory.GetFiles(targetDirectory);
        foreach (var filePath in fileEntries)
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        // Recursively delete all subdirectories
        var subdirectoryEntries = Directory.GetDirectories(targetDirectory);
        foreach (var subdirectoryPath in subdirectoryEntries)
        {
            DeleteDirectory(subdirectoryPath);
        }

        // Delete the target directory itself
        Directory.Delete(targetDirectory, false);
    }

    private async Task UpdateSelectedPackage()
    {
        var package = packageFactory.FindPackageByName(SelectedPackage?.PackageName ?? string.Empty);
        if (package == null)
        {
            logger.LogError($"Could not find package {SelectedPackage.PackageName}");
            return;
        }

        ProgressText = $"Updating {SelectedPackage.DisplayName} to latest version...";
        package.InstallLocation = SelectedPackage.Path!;
        package.UpdateProgressChanged += SelectedPackageOnProgressChanged;
        package.UpdateComplete += (_, _) =>
        {
            SelectedPackageOnProgressChanged(this, 100);
            ProgressText = "Update complete";
            SelectedPackage.UpdateAvailable = false;
            UpdateAvailable = false;
        };
        var updateResult = await package.Update(SelectedPackage);
        settingsManager.UpdatePackageVersionNumber(SelectedPackage.Id, updateResult);
        await OnLoaded();
    }

    [RelayCommand]
    private async Task ShowInstallWindow()
    {
        var installWindow = dialogFactory.CreateInstallerWindow();
        installWindow.ShowDialog();
        await OnLoaded();
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
            ProgressText = $"Updating {SelectedPackage.DisplayName} to latest version... {progress}%";
        }
        
        EventManager.Instance.OnGlobalProgressChanged(progress);
    }
}
