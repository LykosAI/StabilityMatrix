using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

public partial class PackageCardViewModel : ProgressViewModel
{
    private readonly ILogger<PackageCardViewModel> logger;
    private readonly IPackageFactory packageFactory;
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly INavigationService navigationService;
    private readonly ServiceManager<ViewModelBase> vmFactory;

    [ObservableProperty] private InstalledPackage? package;
    [ObservableProperty] private string? cardImageSource;
    [ObservableProperty] private bool isUpdateAvailable;
    [ObservableProperty] private string? installedVersion;
    [ObservableProperty] private bool isUnknownPackage;
    
    public PackageCardViewModel(
        ILogger<PackageCardViewModel> logger,
        IPackageFactory packageFactory,
        INotificationService notificationService, 
        ISettingsManager settingsManager, 
        INavigationService navigationService,
        ServiceManager<ViewModelBase> vmFactory)
    {
        this.logger = logger;
        this.packageFactory = packageFactory;
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.navigationService = navigationService;
        this.vmFactory = vmFactory;
    }

    partial void OnPackageChanged(InstalledPackage? value)
    {
        if (string.IsNullOrWhiteSpace(value?.PackageName))
            return;

        if (value.PackageName == UnknownPackage.Key)
        {
            IsUnknownPackage = true;
            CardImageSource = "";
            InstalledVersion = "Unknown";
        }
        else
        {
            IsUnknownPackage = false;
            
            var basePackage = packageFactory[value.PackageName];
            CardImageSource = basePackage?.PreviewImageUri.ToString() 
                              ?? Assets.NoImage.ToString();
            InstalledVersion = value.DisplayVersion ?? "Unknown";
        }
    }

    public override async Task OnLoadedAsync()
    {
        IsUpdateAvailable = await HasUpdate();
    }

    public void Launch()
    {
        if (Package == null)
            return;
        
        settingsManager.Transaction(s => s.ActiveInstalledPackageId = Package.Id);
        
        navigationService.NavigateTo<LaunchPageViewModel>(new BetterDrillInNavigationTransition());
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

            var packagePath = new DirectoryPath(settingsManager.LibraryDir, Package.LibraryPath);
            var deleteTask = packagePath.DeleteVerboseAsync(logger);
                
            var taskResult = await notificationService.TryAsync(deleteTask,
                "Some files could not be deleted. Please close any open files in the package directory and try again.");
            if (taskResult.IsSuccessful)
            {
                notificationService.Show(new Notification("Success",
                    $"Package {Package.DisplayName} uninstalled",
                    NotificationType.Success));

                if (!IsUnknownPackage)
                {
                    settingsManager.Transaction(settings =>
                    {
                        settings.RemoveInstalledPackageAndUpdateActive(Package);
                    });
                }
                
                EventManager.Instance.OnInstalledPackagesChanged();
            }
        }
    }
    
    public async Task Update()
    {
        if (Package is null || IsUnknownPackage) return;
        
        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
        {
            logger.LogWarning("Could not find package {SelectedPackagePackageName}", 
                Package.PackageName);
            notificationService.Show("Invalid Package type",
                $"Package {Package.PackageName.ToRepr()} is not a valid package type",
                NotificationType.Error);
            return;
        }

        var packageName = Package.DisplayName ?? Package.PackageName ?? "";
        
        Text = $"Updating {packageName}";
        IsIndeterminate = true;
        
        var progressId = Guid.NewGuid();
        EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
            Package.DisplayName ?? Package.PackageName!,
            new ProgressReport(0f, isIndeterminate: true, type: ProgressType.Update)));

        try
        {
            basePackage.InstallLocation = Package.FullPath!;
            
            var progress = new Progress<ProgressReport>(progress =>
            {
                var percent = Convert.ToInt32(progress.Percentage);
            
                Value = percent;
                IsIndeterminate = progress.IsIndeterminate;
                Text = $"Updating {Package.DisplayName}";
            
                EventManager.Instance.OnGlobalProgressChanged(percent);
                EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                    packageName, progress));
            });
        
            var updateResult = await basePackage.Update(Package, progress);
        
            settingsManager.UpdatePackageVersionNumber(Package.Id, updateResult);
            notificationService.Show("Update complete",
                $"{Package.DisplayName} has been updated to the latest version.",
                NotificationType.Success);
        
            await using (settingsManager.BeginTransaction())
            {
                Package.UpdateAvailable = false;
            }
            IsUpdateAvailable = false;
            InstalledVersion = Package.DisplayVersion ?? "Unknown";

            EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                packageName,
                new ProgressReport(1f, "Update complete", type: ProgressType.Update)));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error Updating Package ({PackageName})", basePackage.Name);
            notificationService.ShowPersistent($"Error Updating {Package.DisplayName}", e.Message, NotificationType.Error);
            EventManager.Instance.OnProgressChanged(new ProgressItem(progressId,
                packageName,
                new ProgressReport(0f, "Update failed", type: ProgressType.Update), Failed: true));
        }
        finally
        {
            IsIndeterminate = false;
            Value = 0;
            Text = "";
        }
    }

    public async Task Import()
    {
        if (!IsUnknownPackage || Design.IsDesignMode) return;

        var viewModel = vmFactory.Get<PackageImportViewModel>(vm =>
        {
            vm.PackagePath =
                new DirectoryPath(Package?.FullPath ?? throw new InvalidOperationException());
        });

        var dialog = new TaskDialog
        {
            Content = new PackageImportDialog
            {
                DataContext = viewModel
            },
            ShowProgressBar = false,
            Buttons = new List<TaskDialogButton>
            {
                new(Resources.Action_Import, TaskDialogStandardResult.Yes)
                {
                    IsDefault = true
                },
                new(Resources.Action_Cancel, TaskDialogStandardResult.Cancel)
            }
        };

        dialog.Closing += async (sender, e) =>
        {
            // We only want to use the deferral on the 'Yes' Button
            if ((TaskDialogStandardResult)e.Result == TaskDialogStandardResult.Yes)
            {
                var deferral = e.GetDeferral();

                sender.ShowProgressBar = true;
                sender.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);

                await using (new MinimumDelay(200, 300))
                {
                    var result = await notificationService.TryAsync(viewModel.AddPackageWithCurrentInputs());
                    if (result.IsSuccessful)
                    {
                        EventManager.Instance.OnInstalledPackagesChanged();
                    }
                }

                deferral.Complete();
            }
        };

        dialog.XamlRoot = App.VisualRoot;
        
        await dialog.ShowAsync(true);
    }
    
    public async Task OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(Package?.FullPath))
            return;
        
        await ProcessRunner.OpenFolderBrowser(Package.FullPath);
    }
    
    private async Task<bool> HasUpdate()
    {
        if (Package == null || IsUnknownPackage || Design.IsDesignMode)
            return false;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
            return false;

        var canCheckUpdate = Package.LastUpdateCheck == null ||
                             Package.LastUpdateCheck < DateTime.Now.AddMinutes(-15);

        if (!canCheckUpdate)
        {
            return Package.UpdateAvailable;
        }

        try
        {
            var hasUpdate = await basePackage.CheckForUpdates(Package);
            Package.UpdateAvailable = hasUpdate;
            Package.LastUpdateCheck = DateTimeOffset.Now;
            settingsManager.SetLastUpdateCheck(Package);
            return hasUpdate;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error checking {PackageName} for updates", Package.PackageName);
            return false;
        }
    }
}
