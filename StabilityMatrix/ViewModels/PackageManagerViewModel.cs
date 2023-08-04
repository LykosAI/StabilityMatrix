using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using StabilityMatrix.Helper;
using System.Linq;
using System.Windows;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Polly;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.ContentDialogControl;
using EventManager = StabilityMatrix.Core.Helper.EventManager;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;

namespace StabilityMatrix.ViewModels;

public partial class PackageManagerViewModel : ObservableObject
{
    private readonly ILogger<PackageManagerViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IDialogFactory dialogFactory;
    private readonly IContentDialogService contentDialogService;
    private readonly ISnackbarService snackbarService;
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
        IPackageFactory packageFactory, IDialogFactory dialogFactory, IContentDialogService contentDialogService, ISnackbarService snackbarService)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.dialogFactory = dialogFactory;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;

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
            installedPackages.FirstOrDefault(x => x.Id == settingsManager.Settings.ActiveInstalledPackageId) ??
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
        if (SelectedPackage?.LibraryPath == null)
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
            InstallButtonEnabled = false;
            var deleteTask = DeleteDirectoryAsync(Path.Combine(settingsManager.LibraryDir,
                SelectedPackage.LibraryPath));
            var taskResult = await snackbarService.TryAsync(deleteTask,
                "Some files could not be deleted. Please close any open files in the package directory and try again.");
            if (taskResult.IsSuccessful)
            {
                snackbarService.ShowSnackbarAsync($"Package {SelectedPackage.DisplayName} uninstalled", "Success",
                    ControlAppearance.Success).SafeFireAndForget();

                settingsManager.Transaction(settings =>
                {
                    settings.RemoveInstalledPackageAndUpdateActive(SelectedPackage);
                });
            }
            await OnLoaded();
            IsUninstalling = false;
            InstallButtonEnabled = true;
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
        // Skip if directory does not exist
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }
        // For junction points, delete with recursive false
        if (new DirectoryInfo(targetDirectory).LinkTarget != null)
        {
            logger.LogInformation("Removing junction point {TargetDirectory}", targetDirectory);
            try
            {
                Directory.Delete(targetDirectory, false);
                return;
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to delete junction point {targetDirectory}", ex);
            }
        }
        // Recursively delete all subdirectories
        var subdirectoryEntries = Directory.GetDirectories(targetDirectory);
        foreach (var subdirectoryPath in subdirectoryEntries)
        {
            DeleteDirectory(subdirectoryPath);
        }
        // Delete all files in the directory
        var fileEntries = Directory.GetFiles(targetDirectory);
        foreach (var filePath in fileEntries)
        {
            try
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to delete file {filePath}", ex);
            }
        }
        // Delete the target directory itself
        try
        {
            Directory.Delete(targetDirectory, false);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to delete directory {targetDirectory}", ex);
        }
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
        package.InstallLocation = SelectedPackage.FullPath!;
        var progress = new Progress<ProgressReport>(progress =>
        {
            var percent = Convert.ToInt32(progress.Percentage);
            if (progress.IsIndeterminate || progress.Progress == -1)
            {
                IsIndeterminate = true;
            }
            else
            {
                IsIndeterminate = false;
                ProgressValue = percent;
            }

            ProgressText = $"Updating {SelectedPackage.DisplayName} to latest version... {percent:N0}%";
            EventManager.Instance.OnGlobalProgressChanged(percent);
        });
        var updateResult = await package.Update(SelectedPackage, progress);
        
        ProgressText = "Update complete";
        SelectedPackage.UpdateAvailable = false;
        UpdateAvailable = false;
        
        settingsManager.UpdatePackageVersionNumber(SelectedPackage.Id, updateResult);
        await OnLoaded();
    }

    [RelayCommand]
    private async Task ShowInstallWindow()
    {
        var installWindow = dialogFactory.CreateInstallerWindow();
        if (Application.Current.MainWindow != null)
        {
            installWindow.Owner = Application.Current.MainWindow;
        }
        installWindow.ShowDialog();
        await OnLoaded();
    }
}
