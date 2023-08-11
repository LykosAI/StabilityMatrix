using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using NLog;
using Polly;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

public partial class PackageCardViewModel : ProgressViewModel
{
    private readonly IPackageFactory packageFactory;
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly Logger logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private InstalledPackage? package;
    [ObservableProperty] private Uri cardImage;
    [ObservableProperty] private bool isUpdateAvailable;

    public PackageCardViewModel(IPackageFactory packageFactory,
        INotificationService notificationService, ISettingsManager settingsManager)
    {
        this.packageFactory = packageFactory;
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
    }

    partial void OnPackageChanged(InstalledPackage? value)
    {
        if (string.IsNullOrWhiteSpace(value?.PackageName))
            return;

        var basePackage = packageFactory[value.PackageName];
        CardImage = basePackage?.PreviewImageUri ?? Assets.NoImage;
    }

    public void Launch()
    {
        if (Package == null)
            return;
        
        settingsManager.Transaction(s => s.ActiveInstalledPackageId = Package.Id);
            
        EventManager.Instance.RequestPageChange(typeof(LaunchPageViewModel));
        EventManager.Instance.OnPackageLaunchRequested(Package.Id);
    }
    
    public async Task Uninstall()
    {
        if (Package?.LibraryPath == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Are you sure?",
            Content = "This will delete all folders in the package directory, including any generated images in that directory as well as any files you may have added.",
            PrimaryButtonText = "Yes, delete it",
            CloseButtonText = "No, keep it",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            Text = "Uninstalling...";
            IsIndeterminate = true;
            Value = -1;
            
            var deleteTask = DeleteDirectoryAsync(Path.Combine(settingsManager.LibraryDir,
                Package.LibraryPath));
            var taskResult = await notificationService.TryAsync(deleteTask,
                "Some files could not be deleted. Please close any open files in the package directory and try again.");
            if (taskResult.IsSuccessful)
            {
                notificationService.Show(new Notification("Success",
                    $"Package {Package.DisplayName} uninstalled",
                    NotificationType.Success));
            
                settingsManager.Transaction(settings =>
                {
                    settings.RemoveInstalledPackageAndUpdateActive(Package);
                });
                
                EventManager.Instance.OnInstalledPackagesChanged();
            }
        }
    }
    
    public async Task Update()
    {
        if (Package == null) return;
        var basePackage = packageFactory.FindPackageByName(Package.PackageName);
        if (basePackage == null)
        {
            logger.Error("Could not find package {SelectedPackagePackageName}", 
                Package.PackageName);
            return;
        }

        Text = $"Updating {Package.DisplayName} to latest version...";
        basePackage.InstallLocation = Package.FullPath!;
        var progress = new Progress<ProgressReport>(progress =>
        {
            var percent = Convert.ToInt32(progress.Percentage);
            
            Value = percent;
            IsIndeterminate = progress.IsIndeterminate;
            Text = $"Updating {Package.DisplayName} to latest version... {percent:N0}%";
            
            EventManager.Instance.OnGlobalProgressChanged(percent);
        });
        
        var updateResult = await basePackage.Update(Package, progress);

        if (string.IsNullOrWhiteSpace(updateResult))
        {
            var errorMsg =
                $"There was an error updating {Package.DisplayName}. Please try again later.";

            if (Package.PackageName == "automatic")
            {
                errorMsg = errorMsg.Replace("Please try again later.",
                    "Please stash any changes before updating, or manually update the package.");
            }
            
            // there was an error
            notificationService.Show(new Notification("Error updating package",
                errorMsg, NotificationType.Error));
        }
        
        settingsManager.UpdatePackageVersionNumber(Package.Id, updateResult);
        notificationService.Show("Update complete",
            $"{Package.DisplayName} has been updated to the latest version.",
            NotificationType.Success);
        
        Package.UpdateAvailable = false;
        IsUpdateAvailable = false;
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
                    logger.Warn(
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
            logger.Info("Removing junction point {TargetDirectory}", targetDirectory);
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

    private async Task<bool> HasUpdate()
    {
        if (Package == null) return false;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null) return false;
        
        var canCheckUpdate = Package.LastUpdateCheck == null || 
                             Package.LastUpdateCheck < DateTime.Now.AddMinutes(-15);
        
        if (!canCheckUpdate) return false;
        
        var hasUpdate = await basePackage.CheckForUpdates(Package);
        Package.UpdateAvailable = hasUpdate;
        Package.LastUpdateCheck = DateTimeOffset.Now;
        IsUpdateAvailable = true;
        settingsManager.SetLastUpdateCheck(Package);

        return hasUpdate;

    }
}
