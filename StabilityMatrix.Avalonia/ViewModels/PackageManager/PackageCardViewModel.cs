using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Packages;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[ManagedService]
[Transient]
public partial class PackageCardViewModel(
    ILogger<PackageCardViewModel> logger,
    IPackageFactory packageFactory,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    INavigationService<MainWindowViewModel> navigationService,
    ServiceManager<ViewModelBase> vmFactory,
    RunningPackageService runningPackageService
) : ProgressViewModel
{
    [ObservableProperty]
    private InstalledPackage? package;

    [ObservableProperty]
    private string? cardImageSource;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private string? installedVersion;

    [ObservableProperty]
    private bool isUnknownPackage;

    [ObservableProperty]
    private bool isSharedModelSymlink;

    [ObservableProperty]
    private bool isSharedModelConfig;

    [ObservableProperty]
    private bool isSharedModelDisabled;

    [ObservableProperty]
    private bool canUseConfigMethod;

    [ObservableProperty]
    private bool canUseSymlinkMethod;

    [ObservableProperty]
    private bool useSharedOutput;

    [ObservableProperty]
    private bool canUseSharedOutput;

    [ObservableProperty]
    private bool canUseExtensions;

    partial void OnPackageChanged(InstalledPackage? value)
    {
        if (string.IsNullOrWhiteSpace(value?.PackageName))
            return;

        if (
            value.PackageName == UnknownPackage.Key
            || packageFactory.FindPackageByName(value.PackageName) is null
        )
        {
            IsUnknownPackage = true;
            CardImageSource = "";
            InstalledVersion = "Unknown";
        }
        else
        {
            IsUnknownPackage = false;

            var basePackage = packageFactory[value.PackageName];
            CardImageSource = basePackage?.PreviewImageUri.ToString() ?? Assets.NoImage.ToString();
            InstalledVersion = value.Version?.DisplayVersion ?? "Unknown";
            CanUseConfigMethod =
                basePackage?.AvailableSharedFolderMethods.Contains(SharedFolderMethod.Configuration) ?? false;
            CanUseSymlinkMethod =
                basePackage?.AvailableSharedFolderMethods.Contains(SharedFolderMethod.Symlink) ?? false;
            UseSharedOutput = Package?.UseSharedOutputFolder ?? false;
            CanUseSharedOutput = basePackage?.SharedOutputFolders != null;
            CanUseExtensions = basePackage?.SupportsExtensions ?? false;
        }
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode || !settingsManager.IsLibraryDirSet || Package is not { } currentPackage)
            return;

        if (
            packageFactory.FindPackageByName(currentPackage.PackageName)
            is { } basePackage
                and not UnknownPackage
        )
        {
            // Migrate old packages with null preferred shared folder method
            currentPackage.PreferredSharedFolderMethod ??= basePackage.RecommendedSharedFolderMethod;

            switch (currentPackage.PreferredSharedFolderMethod)
            {
                case SharedFolderMethod.Configuration:
                    IsSharedModelConfig = true;
                    break;
                case SharedFolderMethod.Symlink:
                    IsSharedModelSymlink = true;
                    break;
                case SharedFolderMethod.None:
                    IsSharedModelDisabled = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            IsUpdateAvailable = await HasUpdate();
        }
    }

    public async Task Launch()
    {
        if (Package == null)
            return;

        var packageId = await runningPackageService.StartPackage(Package);

        if (packageId != null)
        {
            var vm = runningPackageService.GetRunningPackageViewModel(packageId.Value);
            if (vm != null)
            {
                navigationService.NavigateTo(vm, new BetterDrillInNavigationTransition());
            }
        }

        // settingsManager.Transaction(s => s.ActiveInstalledPackageId = Package.Id);
        //
        // navigationService.NavigateTo<LaunchPageViewModel>(new BetterDrillInNavigationTransition());
        // EventManager.Instance.OnPackageLaunchRequested(Package.Id);
    }

    public async Task Uninstall()
    {
        if (Package?.LibraryPath == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = Resources.Label_ConfirmDelete,
            Content = Resources.Text_PackageUninstall_Details,
            PrimaryButtonText = Resources.Action_OK,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            Text = Resources.Progress_UninstallingPackage;
            IsIndeterminate = true;
            Value = -1;

            var packagePath = new DirectoryPath(settingsManager.LibraryDir, Package.LibraryPath);
            var deleteTask = packagePath.DeleteVerboseAsync(logger);

            var taskResult = await notificationService.TryAsync(
                deleteTask,
                Resources.Text_SomeFilesCouldNotBeDeleted
            );
            if (taskResult.IsSuccessful)
            {
                notificationService.Show(
                    new Notification(
                        Resources.Label_PackageUninstalled,
                        Package.DisplayName,
                        NotificationType.Success
                    )
                );

                if (!IsUnknownPackage)
                {
                    settingsManager.Transaction(settings =>
                    {
                        settings.RemoveInstalledPackageAndUpdateActive(Package);
                    });
                }

                EventManager.Instance.OnInstalledPackagesChanged();
            }

            Text = "";
            IsIndeterminate = false;
            Value = 0;
        }
    }

    public async Task Update()
    {
        if (Package is null || IsUnknownPackage)
            return;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
        {
            logger.LogWarning("Could not find package {SelectedPackagePackageName}", Package.PackageName);
            notificationService.Show(
                Resources.Label_InvalidPackageType,
                Package.PackageName.ToRepr(),
                NotificationType.Error
            );
            return;
        }

        var packageName = Package.DisplayName ?? Package.PackageName ?? "";

        Text = $"Updating {packageName}";
        IsIndeterminate = true;

        try
        {
            var runner = new PackageModificationRunner
            {
                ModificationCompleteMessage = $"Updated {packageName}",
                ModificationFailedMessage = $"Could not update {packageName}"
            };

            runner.Completed += (_, completedRunner) =>
            {
                notificationService.OnPackageInstallCompleted(completedRunner);
            };

            var versionOptions = new DownloadPackageVersionOptions { IsLatest = true };
            if (Package.Version.IsReleaseMode)
            {
                versionOptions = await basePackage.GetLatestVersion(Package.Version.IsPrerelease);
            }
            else
            {
                var commits = await basePackage.GetAllCommits(Package.Version.InstalledBranch);
                var latest = commits?.FirstOrDefault();
                if (latest == null)
                    throw new Exception("Could not find latest commit");

                versionOptions.BranchName = Package.Version.InstalledBranch;
                versionOptions.CommitHash = latest.Sha;
            }

            var updatePackageStep = new UpdatePackageStep(
                settingsManager,
                Package,
                versionOptions,
                basePackage
            );
            var steps = new List<IPackageStep> { updatePackageStep };

            EventManager.Instance.OnPackageInstallProgressAdded(runner);
            await runner.ExecuteSteps(steps);

            IsUpdateAvailable = false;
            InstalledVersion = Package.Version?.DisplayVersion ?? "Unknown";
            notificationService.Show(
                Resources.Progress_UpdateComplete,
                string.Format(Resources.TextTemplate_PackageUpdatedToLatest, packageName),
                NotificationType.Success
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error Updating Package ({PackageName})", basePackage.Name);
            notificationService.ShowPersistent(
                string.Format(Resources.TextTemplate_ErrorUpdatingPackage, packageName),
                e.Message,
                NotificationType.Error
            );
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
        if (!IsUnknownPackage || Design.IsDesignMode)
            return;

        var viewModel = vmFactory.Get<PackageImportViewModel>(vm =>
        {
            vm.PackagePath = new DirectoryPath(Package?.FullPath ?? throw new InvalidOperationException());
        });

        var dialog = new TaskDialog
        {
            Content = new PackageImportDialog { DataContext = viewModel },
            ShowProgressBar = false,
            Buttons = new List<TaskDialogButton>
            {
                new(Resources.Action_Import, TaskDialogStandardResult.Yes) { IsDefault = true },
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

    [RelayCommand]
    public async Task OpenPythonPackagesDialog()
    {
        if (Package is not { FullPath: not null })
            return;

        var vm = vmFactory.Get<PythonPackagesViewModel>(vm =>
        {
            vm.VenvPath = new DirectoryPath(Package.FullPath, "venv");
        });

        await vm.GetDialog().ShowAsync();
    }

    [RelayCommand]
    public async Task OpenExtensionsDialog()
    {
        if (
            Package is not { FullPath: not null }
            || packageFactory.GetPackagePair(Package) is not { } packagePair
        )
            return;

        var vm = vmFactory.Get<PackageExtensionBrowserViewModel>(vm =>
        {
            vm.PackagePair = packagePair;
        });

        var dialog = new BetterContentDialog
        {
            Content = vm,
            MinDialogWidth = 850,
            MaxDialogHeight = 1100,
            MaxDialogWidth = 850,
            ContentMargin = new Thickness(16, 32),
            CloseOnClickOutside = true,
            FullSizeDesired = true,
            IsFooterVisible = false,
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private void OpenOnGitHub()
    {
        if (Package is null)
            return;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
        {
            logger.LogWarning("Could not find package {SelectedPackagePackageName}", Package.PackageName);
            return;
        }

        ProcessRunner.OpenUrl(basePackage.GithubUrl);
    }

    private async Task<bool> HasUpdate()
    {
        if (Package == null || IsUnknownPackage || Design.IsDesignMode)
            return false;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
            return false;

        var canCheckUpdate =
            Package.LastUpdateCheck == null || Package.LastUpdateCheck < DateTime.Now.AddMinutes(-15);

        if (!canCheckUpdate)
        {
            return Package.UpdateAvailable;
        }

        try
        {
            var hasUpdate = await basePackage.CheckForUpdates(Package);

            await using (settingsManager.BeginTransaction())
            {
                Package.UpdateAvailable = hasUpdate;
                Package.LastUpdateCheck = DateTimeOffset.Now;
            }

            return hasUpdate;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error checking {PackageName} for updates", Package.PackageName);
            return false;
        }
    }

    public void ToggleSharedModelSymlink() => IsSharedModelSymlink = !IsSharedModelSymlink;

    public void ToggleSharedModelConfig() => IsSharedModelConfig = !IsSharedModelConfig;

    public void ToggleSharedModelNone() => IsSharedModelDisabled = !IsSharedModelDisabled;

    public void ToggleSharedOutput() => UseSharedOutput = !UseSharedOutput;

    partial void OnUseSharedOutputChanged(bool value)
    {
        if (Package == null)
            return;

        if (value == Package.UseSharedOutputFolder)
            return;

        using var st = settingsManager.BeginTransaction();
        Package.UseSharedOutputFolder = value;

        var basePackage = packageFactory[Package.PackageName!];
        if (basePackage == null)
            return;

        if (value)
        {
            basePackage.SetupOutputFolderLinks(Package.FullPath!);
        }
        else
        {
            basePackage.RemoveOutputFolderLinks(Package.FullPath!);
        }
    }

    // fake radio button stuff
    partial void OnIsSharedModelSymlinkChanged(bool oldValue, bool newValue)
    {
        if (oldValue == newValue)
            return;

        if (newValue != Package!.PreferredSharedFolderMethod is SharedFolderMethod.Symlink)
        {
            using var st = settingsManager.BeginTransaction();
            Package.PreferredSharedFolderMethod = SharedFolderMethod.Symlink;
        }

        if (newValue)
        {
            IsSharedModelConfig = false;
            IsSharedModelDisabled = false;
        }
        else
        {
            var basePackage = packageFactory[Package!.PackageName!];
            basePackage!.RemoveModelFolderLinks(Package.FullPath!, SharedFolderMethod.Symlink);
        }
    }

    partial void OnIsSharedModelConfigChanged(bool oldValue, bool newValue)
    {
        if (oldValue == newValue)
            return;

        if (newValue != Package!.PreferredSharedFolderMethod is SharedFolderMethod.Configuration)
        {
            using var st = settingsManager.BeginTransaction();
            Package.PreferredSharedFolderMethod = SharedFolderMethod.Configuration;
        }

        if (newValue)
        {
            IsSharedModelSymlink = false;
            IsSharedModelDisabled = false;
        }
        else
        {
            var basePackage = packageFactory[Package!.PackageName!];
            basePackage!.RemoveModelFolderLinks(Package.FullPath!, SharedFolderMethod.Configuration);
        }
    }

    partial void OnIsSharedModelDisabledChanged(bool value)
    {
        if (value)
        {
            if (Package!.PreferredSharedFolderMethod is not SharedFolderMethod.None)
            {
                using var st = settingsManager.BeginTransaction();
                Package.PreferredSharedFolderMethod = SharedFolderMethod.None;
            }

            IsSharedModelSymlink = false;
            IsSharedModelConfig = false;
        }
    }
}
