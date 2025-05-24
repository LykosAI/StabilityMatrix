using System.Collections.Immutable;
using System.Collections.Specialized;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
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
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.PackageManager;

[ManagedService]
[RegisterTransient<PackageCardViewModel>]
public partial class PackageCardViewModel(
    ILogger<PackageCardViewModel> logger,
    IPackageFactory packageFactory,
    INotificationService notificationService,
    ISettingsManager settingsManager,
    INavigationService<PackageManagerViewModel> navigationService,
    IServiceManager<ViewModelBase> vmFactory,
    RunningPackageService runningPackageService
) : ProgressViewModel
{
    private string webUiUrl = string.Empty;
    private string? lastLaunchCommand = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PackageDisplayName))]
    private InstalledPackage? package;

    public string? PackageDisplayName => Package?.DisplayName;

    [ObservableProperty]
    private Uri? cardImageSource;

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

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private bool showWebUiButton;

    [ObservableProperty]
    private DownloadPackageVersionOptions? updateVersion;

    [ObservableProperty]
    private bool dontCheckForUpdates;

    [ObservableProperty]
    private bool usesVenv;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExtraCommands))]
    private List<ExtraPackageCommand>? extraCommands;

    [ObservableProperty]
    private IReadOnlyList<string> extraLaunchCommands = [];

    public bool ShowExtraCommands => ExtraCommands is { Count: > 0 };

    private void RunningPackagesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (runningPackageService.RunningPackages.Select(x => x.Value) is not { } runningPackages)
            return;

        var runningViewModel = runningPackages.FirstOrDefault(x =>
            x.RunningPackage.InstalledPackage.Id == Package?.Id
        );
        if (runningViewModel is not null)
        {
            IsRunning = true;
            runningViewModel.RunningPackage.BasePackage.Exited += BasePackageOnExited;
            runningViewModel.RunningPackage.BasePackage.StartupComplete += RunningPackageOnStartupComplete;
        }
        else if (runningViewModel is null && IsRunning)
        {
            IsRunning = false;
            ShowWebUiButton = false;
        }
    }

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
            CardImageSource = null;
            InstalledVersion = "Unknown";
        }
        else
        {
            IsUnknownPackage = false;

            var basePackage = packageFactory[value.PackageName!];
            CardImageSource = basePackage?.PreviewImageUri ?? Assets.NoImage;
            InstalledVersion = value.Version?.DisplayVersion ?? "Unknown";
            CanUseConfigMethod =
                basePackage?.AvailableSharedFolderMethods.Contains(SharedFolderMethod.Configuration) ?? false;
            CanUseSymlinkMethod =
                basePackage?.AvailableSharedFolderMethods.Contains(SharedFolderMethod.Symlink) ?? false;
            UseSharedOutput = Package?.UseSharedOutputFolder ?? false;
            CanUseSharedOutput = basePackage?.SharedOutputFolders != null;
            CanUseExtensions = basePackage?.SupportsExtensions ?? false;
            DontCheckForUpdates = Package?.DontCheckForUpdates ?? false;
            UsesVenv = basePackage?.UsesVenv ?? true;
            ExtraLaunchCommands = basePackage?.ExtraLaunchCommands ?? [];

            // Set the extra commands if available from the package
            var packageExtraCommands = basePackage?.GetExtraCommands();
            ExtraCommands = packageExtraCommands?.Count > 0 ? packageExtraCommands : null;

            runningPackageService.RunningPackages.CollectionChanged += RunningPackagesOnCollectionChanged;
            EventManager.Instance.PackageRelaunchRequested += InstanceOnPackageRelaunchRequested;
        }
    }

    private async Task InstanceOnPackageRelaunchRequested(
        object? sender,
        InstalledPackage e,
        RunPackageOptions options
    )
    {
        if (e.Id != Package?.Id)
            return;

        navigationService.GoBack();
        await Launch(options.Command);
    }

    public override async Task OnLoadedAsync()
    {
        if (Design.IsDesignMode && Package?.DisplayName == "Running Comfy")
        {
            IsRunning = true;
            IsUpdateAvailable = true;
            ShowWebUiButton = true;
        }

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
            if (IsUpdateAvailable)
            {
                UpdateVersion = await basePackage.GetUpdate(currentPackage);
            }

            if (
                Package != null
                && !IsRunning
                && runningPackageService.RunningPackages.TryGetValue(Package.Id, out var runningPackageVm)
            )
            {
                IsRunning = true;
                runningPackageVm.RunningPackage.BasePackage.Exited += BasePackageOnExited;
                runningPackageVm.RunningPackage.BasePackage.StartupComplete +=
                    RunningPackageOnStartupComplete;
                webUiUrl = runningPackageVm.WebUiUrl;
                ShowWebUiButton = !string.IsNullOrWhiteSpace(webUiUrl);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        EventManager.Instance.PackageRelaunchRequested -= InstanceOnPackageRelaunchRequested;
        runningPackageService.RunningPackages.CollectionChanged -= RunningPackagesOnCollectionChanged;

        // Cleanup any running package event handlers
        if (
            Package?.Id != null
            && runningPackageService.RunningPackages.TryGetValue(Package.Id, out var runningPackageVm)
        )
        {
            runningPackageVm.RunningPackage.BasePackage.Exited -= BasePackageOnExited;
            runningPackageVm.RunningPackage.BasePackage.StartupComplete -= RunningPackageOnStartupComplete;
        }
    }

    public async Task Launch(string? command = null)
    {
        if (Package == null)
            return;

        var packagePair = await runningPackageService.StartPackage(Package, command);

        if (packagePair != null)
        {
            IsRunning = true;
            lastLaunchCommand = command;

            packagePair.BasePackage.Exited += BasePackageOnExited;
            packagePair.BasePackage.StartupComplete += RunningPackageOnStartupComplete;

            var vm = runningPackageService.GetRunningPackageViewModel(packagePair.InstalledPackage.Id);
            if (vm != null)
            {
                navigationService.NavigateTo(vm, new BetterEntranceNavigationTransition());
            }
        }

        // settingsManager.Transaction(s => s.ActiveInstalledPackageId = Package.Id);
        //
        // navigationService.NavigateTo<LaunchPageViewModel>(new BetterDrillInNavigationTransition());
        // EventManager.Instance.OnPackageLaunchRequested(Package.Id);
    }

    public void NavToConsole()
    {
        if (Package == null)
            return;

        var vm = runningPackageService.GetRunningPackageViewModel(Package.Id);
        if (vm != null)
        {
            navigationService.NavigateTo(vm, new BetterEntranceNavigationTransition());
        }
    }

    public void LaunchWebUi()
    {
        if (string.IsNullOrEmpty(webUiUrl))
            return;

        notificationService.TryAsync(
            Task.Run(() => ProcessRunner.OpenUrl(webUiUrl)),
            "Failed to open URL",
            $"{webUiUrl}"
        );
    }

    private void BasePackageOnExited(object? sender, int exitCode)
    {
        Dispatcher
            .UIThread.InvokeAsync(async () =>
            {
                logger.LogTrace("Process exited ({Code}) at {Time:g}", exitCode, DateTimeOffset.Now);

                // Need to wait for streams to finish before detaching handlers
                if (sender is BaseGitPackage { VenvRunner: not null } package)
                {
                    var process = package.VenvRunner.Process;
                    if (process is not null)
                    {
                        // Max 5 seconds
                        var ct = new CancellationTokenSource(5000).Token;
                        try
                        {
                            await process.WaitUntilOutputEOF(ct);
                        }
                        catch (OperationCanceledException e)
                        {
                            logger.LogWarning("Waiting for process EOF timed out: {Message}", e.Message);
                        }
                    }
                }

                // Detach handlers
                if (sender is BasePackage basePackage)
                {
                    basePackage.Exited -= BasePackageOnExited;
                    basePackage.StartupComplete -= RunningPackageOnStartupComplete;
                }

                if (Package?.Id != null)
                {
                    runningPackageService.RunningPackages.Remove(Package.Id);
                }

                IsRunning = false;
                ShowWebUiButton = false;
            })
            .SafeFireAndForget();
    }

    public async Task Uninstall()
    {
        if (Package?.LibraryPath == null)
        {
            return;
        }

        var dialogViewModel = vmFactory.Get<ConfirmPackageDeleteDialogViewModel>(vm =>
        {
            vm.ExpectedPackageName = Package?.DisplayName;
        });

        var dialog = new BetterContentDialog
        {
            Content = dialogViewModel,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
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
                ModificationFailedMessage = $"Could not update {packageName}",
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
                basePackage,
                Package.FullPath!.Unwrap(),
                Package,
                new UpdatePackageOptions
                {
                    VersionOptions = versionOptions,
                    PythonOptions = { TorchIndex = Package.PreferredTorchIndex },
                }
            );
            var steps = new List<IPackageStep> { updatePackageStep };

            EventManager.Instance.OnPackageInstallProgressAdded(runner);
            await runner.ExecuteSteps(steps);

            IsUpdateAvailable = false;
            InstalledVersion = Package.Version?.DisplayVersion ?? "Unknown";
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
                new(Resources.Action_Cancel, TaskDialogStandardResult.Cancel),
            },
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
    private async Task ChangeVersion()
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
            var viewModel = vmFactory.Get<PackageImportViewModel>(vm =>
            {
                vm.PackagePath = new DirectoryPath(
                    Package?.FullPath ?? throw new InvalidOperationException()
                );
            });

            viewModel.SelectedBasePackage = basePackage;
            viewModel.CanSelectBasePackage = false;
            viewModel.IsReleaseMode = Package.Version?.IsReleaseMode ?? false;

            var dialog = new TaskDialog
            {
                Content = new PackageImportDialog { DataContext = viewModel },
                ShowProgressBar = false,
                Buttons = new List<TaskDialogButton>
                {
                    new(Resources.Action_Update, TaskDialogStandardResult.Yes) { IsDefault = true },
                    new(Resources.Action_Cancel, TaskDialogStandardResult.Cancel),
                },
                XamlRoot = App.VisualRoot,
            };

            var result = await dialog.ShowAsync(true);
            if (result is not TaskDialogStandardResult.Yes)
                return;

            var runner = new PackageModificationRunner
            {
                ModificationCompleteMessage = $"Updated {packageName}",
                ModificationFailedMessage = $"Could not update {packageName}",
            };

            var versionOptions = new DownloadPackageVersionOptions();

            if (!string.IsNullOrWhiteSpace(viewModel.CustomCommitSha))
            {
                versionOptions.BranchName = viewModel.SelectedVersion?.TagName;
                versionOptions.CommitHash = viewModel.CustomCommitSha;
            }
            else if (viewModel.SelectedVersionType == PackageVersionType.GithubRelease)
            {
                versionOptions.VersionTag = viewModel.SelectedVersion?.TagName;
            }
            else
            {
                versionOptions.BranchName = viewModel.SelectedVersion?.TagName;
                versionOptions.CommitHash = viewModel.SelectedCommit?.Sha;
            }

            var updatePackageStep = new UpdatePackageStep(
                settingsManager,
                basePackage,
                Package.FullPath!.Unwrap(),
                Package,
                new UpdatePackageOptions
                {
                    VersionOptions = versionOptions,
                    PythonOptions = { TorchIndex = Package.PreferredTorchIndex },
                }
            );
            var steps = new List<IPackageStep> { updatePackageStep };

            EventManager.Instance.OnPackageInstallProgressAdded(runner);
            await runner.ExecuteSteps(steps);

            EventManager.Instance.OnInstalledPackagesChanged();
            IsUpdateAvailable = false;
            InstalledVersion = Package.Version?.DisplayVersion ?? "Unknown";

            if (runner.Failed)
            {
                notificationService.Show(
                    Resources.Progress_UpdateFailed,
                    string.Format(runner.ModificationFailedMessage, packageName),
                    NotificationType.Error
                );
            }
            else
            {
                notificationService.Show(
                    Resources.Progress_UpdateComplete,
                    string.Format(Resources.TextTemplate_PackageUpdatedToSelected, packageName),
                    NotificationType.Success
                );
            }
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

    [RelayCommand]
    public async Task OpenPythonPackagesDialog()
    {
        if (Package is not { FullPath: not null })
            return;

        var vm = vmFactory.Get<PythonPackagesViewModel>(vm =>
        {
            vm.VenvPath = new DirectoryPath(Package.FullPath, "venv");
            vm.PythonVersion = PyVersion.Parse(Package.PythonVersion);
        });

        await vm.GetDialog().ShowAsync();
    }

    [RelayCommand]
    public async Task OpenPythonDependenciesOverrideDialog()
    {
        if (Package is not { FullPath: not null })
            return;

        var vm = vmFactory.Get<PythonPackageSpecifiersViewModel>();

        vm.LoadSpecifiers(Package.PipOverrides ?? []);

        if (await vm.GetDialog().ShowAsync() is ContentDialogResult.Primary)
        {
            await using var st = settingsManager.BeginTransaction();
            Package.PipOverrides = vm.GetSpecifiers().ToList();
        }
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
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
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

    [RelayCommand]
    private async Task Stop()
    {
        if (Package is null)
            return;

        await runningPackageService.StopPackage(Package.Id);
        IsRunning = false;
        ShowWebUiButton = false;
    }

    [RelayCommand]
    private async Task Restart()
    {
        await Stop();
        await Launch(lastLaunchCommand);
    }

    [RelayCommand]
    private async Task ShowLaunchOptions()
    {
        var basePackage = packageFactory.FindPackageByName(Package?.PackageName);
        if (basePackage == null)
        {
            logger.LogWarning("Package {Name} not found", Package?.PackageName);
            return;
        }

        var viewModel = vmFactory.Get<LaunchOptionsViewModel>();
        viewModel.Cards = LaunchOptionCard
            .FromDefinitions(basePackage.LaunchOptions, Package?.LaunchArgs ?? [])
            .ToImmutableArray();

        logger.LogDebug("Launching config dialog with cards: {CardsCount}", viewModel.Cards.Count);

        var dialog = new BetterContentDialog
        {
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = Resources.Action_Save,
            CloseButtonText = Resources.Action_Cancel,
            FullSizeDesired = true,
            DefaultButton = ContentDialogButton.Primary,
            ContentMargin = new Thickness(32, 16),
            Padding = new Thickness(0, 16),
            Content = new LaunchOptionsDialog { DataContext = viewModel },
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && Package != null)
        {
            // Save config
            var args = viewModel.AsLaunchArgs();
            settingsManager.SaveLaunchArgs(Package.Id, args);
        }
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (Package is null || IsUnknownPackage)
            return;

        var currentName = Package.DisplayName ?? Package.PackageName ?? string.Empty;
        var field = new TextBoxField
        {
            Label = Resources.Label_DisplayName,
            Text = currentName,
            Watermark = Resources.Watermark_EnterPackageName,
            Validator = text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new DataValidationException(Resources.Validation_PackageNameCannotBeEmpty);
                }

                var directoryPath = new DirectoryPath(Path.GetDirectoryName(Package.FullPath!)!, text);
                if (directoryPath.Exists)
                {
                    throw new DataValidationException(
                        string.Format(Resources.ValidationError_PackageExists, text)
                    );
                }
            },
        };

        var result = await DialogHelper.GetTextEntryDialogResultAsync(
            field,
            string.Format(Resources.Description_RenamePackage, currentName)
        );

        if (result.Result == ContentDialogResult.Primary && field.IsValid && field.Text != currentName)
        {
            var newPackagePath = new DirectoryPath(Path.GetDirectoryName(Package.FullPath!)!, field.Text);
            var existingPath = new DirectoryPath(Package.FullPath!);
            if (existingPath.FullPath == newPackagePath.FullPath)
                return;

            try
            {
                await existingPath.MoveToAsync(newPackagePath);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to rename package directory from {OldPath} to {NewPath}",
                    existingPath.FullPath,
                    newPackagePath.FullPath
                );
                notificationService.Show(
                    Resources.Label_UnexpectedErrorOccurred,
                    ex.Message,
                    NotificationType.Error
                );
                return;
            }

            Package.DisplayName = field.Text;
            OnPropertyChanged(nameof(PackageDisplayName));
            settingsManager.Transaction(s =>
            {
                var packageToUpdate = s.InstalledPackages.FirstOrDefault(p => p.Id == Package.Id);
                if (packageToUpdate != null)
                {
                    packageToUpdate.DisplayName = field.Text;
                    packageToUpdate.LibraryPath = Path.Combine("Packages", field.Text);
                }
            });
        }
    }

    [RelayCommand]
    private async Task ExecuteExtraCommand(string commandName)
    {
        var command = ExtraCommands?.FirstOrDefault(cmd => cmd.CommandName == commandName);
        if (command == null)
            return;

        Text = $"Executing {commandName}...";
        IsIndeterminate = true;
        Value = -1;

        try
        {
            await command.Command(Package!);
            notificationService.Show("Command executed successfully", commandName, NotificationType.Success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command {CommandName}", commandName);
            notificationService.ShowPersistent(
                $"Error during {commandName} operation",
                ex.Message,
                NotificationType.Error
            );
        }
        finally
        {
            Text = "";
            IsIndeterminate = false;
            Value = 0;
        }
    }

    private async Task<bool> HasUpdate()
    {
        if (Package == null || IsUnknownPackage || Design.IsDesignMode || Package.DontCheckForUpdates)
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
            UpdateVersion = await basePackage.GetUpdate(Package);

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

    public void ToggleDontCheckForUpdates() => DontCheckForUpdates = !DontCheckForUpdates;

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

    partial void OnDontCheckForUpdatesChanged(bool value)
    {
        if (value)
        {
            UpdateVersion = null;
            IsUpdateAvailable = false;

            if (Package == null)
                return;

            Package.UpdateAvailable = false;
            settingsManager.Transaction(s =>
            {
                s.SetUpdateCheckDisabledForPackage(Package, value);
            });
        }
        else if (Package != null)
        {
            Package.LastUpdateCheck = DateTimeOffset.MinValue;
            settingsManager.Transaction(s =>
            {
                s.SetUpdateCheckDisabledForPackage(Package, value);
            });
            OnLoadedAsync().SafeFireAndForget();
        }
    }

    private void RunningPackageOnStartupComplete(object? sender, string e)
    {
        webUiUrl = e.Replace("0.0.0.0", "127.0.0.1");
        ShowWebUiButton = !string.IsNullOrWhiteSpace(webUiUrl);
    }
}
