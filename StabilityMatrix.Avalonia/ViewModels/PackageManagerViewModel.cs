using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Polly;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
///  This is our ViewModel for the second page
/// </summary>

[View(typeof(PackageManagerPage))]
public partial class PackageManagerViewModel : PageViewModelBase
{
    private readonly ILogger<PackageManagerViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly INotificationService notificationService;
    private readonly ServiceManager<ViewModelBase> dialogFactory;

    private const int MinutesToWaitForUpdateCheck = 60;

    public PackageManagerViewModel(ILogger<PackageManagerViewModel> logger,
        ISettingsManager settingsManager, IPackageFactory packageFactory,
        INotificationService notificationService, ServiceManager<ViewModelBase> dialogFactory)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.notificationService = notificationService;
        this.dialogFactory = dialogFactory;
        
        Packages =
            new ObservableCollection<InstalledPackage>(settingsManager.Settings.InstalledPackages);

        SelectedPackage = Packages.FirstOrDefault();
    }

    [ObservableProperty]
    private InstalledPackage? selectedPackage;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
    private int progressValue;
    
    [ObservableProperty]
    private string progressText = string.Empty;
    
    [ObservableProperty]
    private bool isIndeterminate;

    [ObservableProperty] 
    private bool isUninstalling;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedPackage))]
    private bool updateAvailable;
    
    public bool ProgressBarVisibility => ProgressValue > 0 || IsIndeterminate;
    public ObservableCollection<InstalledPackage> Packages { get; }

    public override bool CanNavigateNext { get; protected set; } = true;
    public override bool CanNavigatePrevious { get; protected set; }
    public override string Title => "Packages";
    public override IconSource IconSource => new SymbolIconSource { Symbol = Symbol.Box, IsFilled = true};

    public override async Task OnLoadedAsync()
    {
        if (!Design.IsDesignMode)
        {
            await CheckUpdates();
        }

        var installedPackages = settingsManager.Settings.InstalledPackages;

        SelectedPackage = installedPackages.FirstOrDefault(x =>
            x.Id == settingsManager.Settings.ActiveInstalledPackage);
        SelectedPackage ??= installedPackages.FirstOrDefault();
    }

    private async Task CheckUpdates()
    {
        var installedPackages = settingsManager.Settings.InstalledPackages;
        Packages.Clear();
        foreach (var packageToUpdate in installedPackages)
        {
            var basePackage = packageFactory.FindPackageByName(packageToUpdate.PackageName);
            if (basePackage == null) continue;
            
            var canCheckUpdate = packageToUpdate.LastUpdateCheck == null ||
                                 packageToUpdate.LastUpdateCheck.Value.AddMinutes(MinutesToWaitForUpdateCheck) <
                                 DateTimeOffset.Now;
            if (canCheckUpdate)
            {
                var hasUpdate = await basePackage.CheckForUpdates(packageToUpdate);
                packageToUpdate.UpdateAvailable = hasUpdate;
                packageToUpdate.LastUpdateCheck = DateTimeOffset.Now;
                settingsManager.SetLastUpdateCheck(packageToUpdate);
            }

            Packages.Add(packageToUpdate);
        }
    }

    [RelayCommand]
    private void Launch()
    {
        if (SelectedPackage == null) return;
        EventManager.Instance.RequestPageChange(typeof(LaunchPageViewModel));
        EventManager.Instance.OnPackageLaunchRequested(SelectedPackage.Id);
    }
    
    [RelayCommand]
    private async Task Update()
    {
        await UpdateSelectedPackage();
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        // In design mode, just remove the package from the list
        if (Design.IsDesignMode)
        {
            if (SelectedPackage != null)
            {
                Packages.Remove(SelectedPackage);
            }
            return;
        }
        
        if (SelectedPackage?.LibraryPath == null)
        {
            logger.LogError("No package selected to uninstall");
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
            IsUninstalling = true;
            // InstallButtonEnabled = false;
            var deleteTask = DeleteDirectoryAsync(Path.Combine(settingsManager.LibraryDir,
                SelectedPackage.LibraryPath));
            var taskResult = await notificationService.TryAsync(deleteTask,
                "Some files could not be deleted. Please close any open files in the package directory and try again.");
            if (taskResult.IsSuccessful)
            {
                notificationService.Show(new Notification("Success",
                    $"Package {SelectedPackage.DisplayName} uninstalled",
                    NotificationType.Success));
            
                settingsManager.Transaction(settings =>
                {
                    settings.RemoveInstalledPackageAndUpdateActive(SelectedPackage);
                });
            }
            await OnLoadedAsync();
            IsUninstalling = false;
            // InstallButtonEnabled = true;
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
        var package = packageFactory.FindPackageByName(SelectedPackage.PackageName);
        if (package == null)
        {
            logger.LogError("Could not find package {SelectedPackagePackageName}", 
                SelectedPackage.PackageName);
            return;
        }

        ProgressText = $"Updating {SelectedPackage.DisplayName} to latest version...";
        package.InstallLocation = SelectedPackage.FullPath!;
        var progress = new Progress<ProgressReport>(progress =>
        {
            var percent = Convert.ToInt32(progress.Percentage);
            
            ProgressValue = percent;
            IsIndeterminate = progress.IsIndeterminate;
            ProgressText = $"Updating {SelectedPackage.DisplayName} to latest version... {percent:N0}%";
            
            EventManager.Instance.OnGlobalProgressChanged(percent);
        });
        var updateResult = await package.Update(SelectedPackage, progress);
        
        ProgressText = "Update complete";
        SelectedPackage.UpdateAvailable = false;
        UpdateAvailable = false;
        
        settingsManager.UpdatePackageVersionNumber(SelectedPackage.Id, updateResult);
        await OnLoadedAsync();
    }

    [RelayCommand]
    private async Task ShowInstallDialog()
    {
        var viewModel = dialogFactory.Get<InstallerViewModel>();
        viewModel.AvailablePackages = packageFactory.GetAllAvailablePackages().ToImmutableArray();
        viewModel.SelectedPackage = viewModel.AvailablePackages[0];

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 700,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new InstallerDialog
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowAsync();
    }
}
