using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using NLog;
using SkiaSharp;
using StabilityMatrix.Avalonia.Animations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Avalonia.Views.Settings;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.HardwareInfo;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.PackageModification;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[View(typeof(MainSettingsPage))]
[Singleton, ManagedService]
public partial class MainSettingsViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly ICompletionProvider completionProvider;
    private readonly ITrackedDownloadService trackedDownloadService;
    private readonly IModelIndexService modelIndexService;
    private readonly INavigationService<SettingsViewModel> settingsNavigationService;
    private readonly IAccountsService accountsService;

    public SharedState SharedState { get; }

    public bool IsMacOS => Compat.IsMacOS;

    public override string Title => "Settings";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Settings, IconVariant = IconVariant.Filled };

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public string AppVersion =>
        $"Version {Compat.AppVersion.ToDisplayString()}" + (Program.IsDebugBuild ? " (Debug)" : "");

    // Theme section
    [ObservableProperty]
    private string? selectedTheme;

    public IReadOnlyList<string> AvailableThemes { get; } = new[] { "Light", "Dark", "System", };

    [ObservableProperty]
    private CultureInfo selectedLanguage;

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public IReadOnlyList<CultureInfo> AvailableLanguages => Cultures.SupportedCultures;

    [ObservableProperty]
    private NumberFormatMode selectedNumberFormatMode;

    public IReadOnlyList<NumberFormatMode> NumberFormatModes { get; } =
        Enum.GetValues<NumberFormatMode>().Where(mode => mode != default).ToList();

    public IReadOnlyList<float> AnimationScaleOptions { get; } =
        new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, };

    public IReadOnlyList<HolidayMode> HolidayModes { get; } = Enum.GetValues<HolidayMode>().ToList();

    [ObservableProperty]
    private float selectedAnimationScale;

    // Shared folder options
    [ObservableProperty]
    private bool removeSymlinksOnShutdown;

    // Integrations section
    [ObservableProperty]
    private bool isDiscordRichPresenceEnabled;

    // Console section
    [ObservableProperty]
    private int consoleLogHistorySize;

    // Debug section
    [ObservableProperty]
    private string? debugPaths;

    [ObservableProperty]
    private string? debugCompatInfo;

    [ObservableProperty]
    private string? debugGpuInfo;

    [ObservableProperty]
    private HolidayMode holidayModeSetting;

    [ObservableProperty]
    private bool infinitelyScrollWorkflowBrowser;

    [ObservableProperty]
    private bool autoLoadCivitModels;

    [ObservableProperty]
    private bool moveFilesOnImport;

    #region System Info

    private static Lazy<IReadOnlyList<GpuInfo>> GpuInfosLazy { get; } =
        new(() => HardwareHelper.IterGpuInfo().ToImmutableArray());

    public static IReadOnlyList<GpuInfo> GpuInfos => GpuInfosLazy.Value;

    [ObservableProperty]
    private MemoryInfo memoryInfo;

    private readonly DispatcherTimer hardwareInfoUpdateTimer =
        new() { Interval = TimeSpan.FromSeconds(2.627) };

    public Task<CpuInfo> CpuInfoAsync => HardwareHelper.GetCpuInfoAsync();

    #endregion

    // Info section
    private const int VersionTapCountThreshold = 7;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(VersionFlyoutText))]
    private int versionTapCount;

    [ObservableProperty]
    private bool isVersionTapTeachingTipOpen;
    public string VersionFlyoutText =>
        $"You are {VersionTapCountThreshold - VersionTapCount} clicks away from enabling Debug options.";

    public string DataDirectory => settingsManager.IsLibraryDirSet ? settingsManager.LibraryDir : "Not set";

    public MainSettingsViewModel(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner,
        ServiceManager<ViewModelBase> dialogFactory,
        ITrackedDownloadService trackedDownloadService,
        SharedState sharedState,
        ICompletionProvider completionProvider,
        IModelIndexService modelIndexService,
        INavigationService<SettingsViewModel> settingsNavigationService,
        IAccountsService accountsService
    )
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
        this.dialogFactory = dialogFactory;
        this.trackedDownloadService = trackedDownloadService;
        this.completionProvider = completionProvider;
        this.modelIndexService = modelIndexService;
        this.settingsNavigationService = settingsNavigationService;
        this.accountsService = accountsService;

        SharedState = sharedState;

        if (Program.Args.DebugMode)
        {
            SharedState.IsDebugMode = true;
        }

        SelectedTheme = settingsManager.Settings.Theme ?? AvailableThemes[1];
        SelectedLanguage = Cultures.GetSupportedCultureOrDefault(settingsManager.Settings.Language);
        RemoveSymlinksOnShutdown = settingsManager.Settings.RemoveFolderLinksOnShutdown;
        SelectedAnimationScale = settingsManager.Settings.AnimationScale;
        HolidayModeSetting = settingsManager.Settings.HolidayModeSetting;

        settingsManager.RelayPropertyFor(this, vm => vm.SelectedTheme, settings => settings.Theme);

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsDiscordRichPresenceEnabled,
            settings => settings.IsDiscordRichPresenceEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedAnimationScale,
            settings => settings.AnimationScale
        );
        settingsManager.RelayPropertyFor(
            this,
            vm => vm.HolidayModeSetting,
            settings => settings.HolidayModeSetting
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.InfinitelyScrollWorkflowBrowser,
            settings => settings.IsWorkflowInfiniteScrollEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.SelectedNumberFormatMode,
            settings => settings.NumberFormatMode,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.AutoLoadCivitModels,
            settings => settings.AutoLoadCivitModels,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.MoveFilesOnImport,
            settings => settings.MoveFilesOnImport,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.ConsoleLogHistorySize,
            settings => settings.ConsoleLogHistorySize,
            true
        );

        DebugThrowAsyncExceptionCommand.WithNotificationErrorHandler(notificationService, LogLevel.Warn);

        hardwareInfoUpdateTimer.Tick += OnHardwareInfoUpdateTimerTick;
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();

        hardwareInfoUpdateTimer.Start();
    }

    /// <inheritdoc />
    public override void OnUnloaded()
    {
        base.OnUnloaded();

        hardwareInfoUpdateTimer.Stop();
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        await notificationService.TryAsync(completionProvider.Setup());

        // Start accounts update
        accountsService
            .RefreshAsync()
            .SafeFireAndForget(ex =>
            {
                Logger.Error(ex, "Failed to refresh accounts");
                notificationService.ShowPersistent(
                    "Failed to update account status",
                    ex.ToString(),
                    NotificationType.Error
                );
            });
    }

    private void OnHardwareInfoUpdateTimerTick(object? sender, EventArgs e)
    {
        if (HardwareHelper.IsMemoryInfoAvailable && HardwareHelper.TryGetMemoryInfo(out var newMemoryInfo))
        {
            MemoryInfo = newMemoryInfo;
        }

        // Stop timer if live memory info is not available
        if (!HardwareHelper.IsLiveMemoryUsageInfoAvailable)
        {
            (sender as DispatcherTimer)?.Stop();
        }
    }

    partial void OnSelectedThemeChanged(string? value)
    {
        // In case design / tests
        if (Application.Current is null)
            return;
        // Change theme
        Application.Current.RequestedThemeVariant = value switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    partial void OnSelectedLanguageChanged(CultureInfo? oldValue, CultureInfo newValue)
    {
        if (oldValue is null || newValue.Name == Cultures.Current?.Name)
            return;

        // Set locale
        if (AvailableLanguages.Contains(newValue))
        {
            Logger.Info("Changing language from {Old} to {New}", oldValue, newValue);

            Cultures.TrySetSupportedCulture(newValue, settingsManager.Settings.NumberFormatMode);
            settingsManager.Transaction(s => s.Language = newValue.Name);

            var dialog = new BetterContentDialog
            {
                Title = Resources.Label_RelaunchRequired,
                Content = Resources.Text_RelaunchRequiredToApplyLanguage,
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonText = Resources.Action_Relaunch,
                CloseButtonText = Resources.Action_RelaunchLater
            };

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    // Start the new app while passing our own PID to wait for exit
                    Process.Start(Compat.AppCurrentPath, $"--wait-for-exit-pid {Environment.ProcessId}");

                    // Shutdown the current app
                    App.Shutdown();
                }
            });
        }
        else
        {
            Logger.Info("Requested invalid language change from {Old} to {New}", oldValue, newValue);
        }
    }

    partial void OnRemoveSymlinksOnShutdownChanged(bool value)
    {
        settingsManager.Transaction(s => s.RemoveFolderLinksOnShutdown = value);
    }

    public async Task ResetCheckpointCache()
    {
        await notificationService.TryAsync(modelIndexService.RefreshIndex());

        notificationService.Show(
            "Checkpoint cache reset",
            "The checkpoint cache has been reset.",
            NotificationType.Success
        );
    }

    [RelayCommand]
    private void NavigateToSubPage(Type viewModelType)
    {
        Dispatcher.UIThread.Post(
            () =>
                settingsNavigationService.NavigateTo(
                    viewModelType,
                    BetterSlideNavigationTransition.PageSlideFromRight
                ),
            DispatcherPriority.Send
        );
    }

    #region Package Environment

    [RelayCommand]
    private async Task OpenEnvVarsDialog()
    {
        var viewModel = dialogFactory.Get<EnvVarsViewModel>();

        // Load current settings
        var current = settingsManager.Settings.UserEnvironmentVariables ?? new Dictionary<string, string>();
        viewModel.EnvVars = new ObservableCollection<EnvVarKeyPair>(
            current.Select(kvp => new EnvVarKeyPair(kvp.Key, kvp.Value))
        );

        var dialog = new BetterContentDialog
        {
            Content = new EnvVarsDialog { DataContext = viewModel },
            PrimaryButtonText = Resources.Action_Save,
            IsPrimaryButtonEnabled = true,
            CloseButtonText = Resources.Action_Cancel,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            // Save settings
            var newEnvVars = viewModel
                .EnvVars.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                .GroupBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
            settingsManager.Transaction(s => s.UserEnvironmentVariables = newEnvVars);
        }
    }

    [RelayCommand]
    private async Task CheckPythonVersion()
    {
        var isInstalled = prerequisiteHelper.IsPythonInstalled;
        Logger.Debug($"Check python installed: {isInstalled}");
        // Ensure python installed
        if (!prerequisiteHelper.IsPythonInstalled)
        {
            // Need 7z as well for site packages repack
            Logger.Debug("Python not installed, unpacking resources...");
            await prerequisiteHelper.UnpackResourcesIfNecessary();
            Logger.Debug("Unpacked resources, installing python...");
            await prerequisiteHelper.InstallPythonIfNecessary();
        }

        // Get python version
        await pyRunner.Initialize();
        var result = await pyRunner.GetVersionInfo();
        // Show dialog box
        var dialog = new ContentDialog
        {
            Title = Resources.Label_PythonVersionInfo,
            Content = result,
            PrimaryButtonText = Resources.Action_OK,
            IsPrimaryButtonEnabled = true
        };
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task RunPythonProcess()
    {
        await prerequisiteHelper.UnpackResourcesIfNecessary();
        await prerequisiteHelper.InstallPythonIfNecessary();

        var processPath = new FilePath(PyRunner.PythonExePath);

        if (
            await DialogHelper.GetTextEntryDialogResultAsync(
                new TextBoxField { Label = "Arguments", InnerLeftText = processPath.Name },
                title: "Run Python"
            )
            is not { IsPrimary: true } dialogResult
        )
        {
            return;
        }

        var step = new ProcessStep
        {
            FileName = processPath,
            Args = dialogResult.Value.Text,
            WorkingDirectory = Compat.AppCurrentDir,
            EnvironmentVariables = settingsManager.Settings.EnvironmentVariables.ToImmutableDictionary()
        };

        ConsoleProcessRunner.RunProcessStepAsync(step).SafeFireAndForget();
    }

    [RelayCommand]
    private async Task RunGitProcess()
    {
        await prerequisiteHelper.InstallGitIfNecessary();

        FilePath processPath;

        if (Compat.IsWindows)
        {
            processPath = new FilePath(prerequisiteHelper.GitBinPath, "git.exe");
        }
        else
        {
            var whichGitResult = await ProcessRunner.RunBashCommand(["which", "git"]).EnsureSuccessExitCode();
            processPath = new FilePath(whichGitResult.StandardOutput?.Trim() ?? "git");
        }

        if (
            await DialogHelper.GetTextEntryDialogResultAsync(
                new TextBoxField { Label = "Arguments", InnerLeftText = "git" },
                title: "Run Git"
            )
            is not { IsPrimary: true } dialogResult
        )
        {
            return;
        }

        var step = new ProcessStep
        {
            FileName = processPath,
            Args = dialogResult.Value.Text,
            WorkingDirectory = Compat.AppCurrentDir,
            EnvironmentVariables = settingsManager.Settings.EnvironmentVariables.ToImmutableDictionary()
        };

        ConsoleProcessRunner.RunProcessStepAsync(step).SafeFireAndForget();
    }

    [RelayCommand]
    private async Task FixGitLongPaths()
    {
        var result = await prerequisiteHelper.FixGitLongPaths();
        if (result)
        {
            notificationService.Show(
                "Long Paths Enabled",
                "Git long paths have been enabled.",
                NotificationType.Success
            );
        }
        else
        {
            notificationService.Show(
                "Long Paths Not Enabled",
                "Could not enable Git long paths.",
                NotificationType.Error
            );
        }
    }

    #endregion

    #region Directory Shortcuts

    public CommandItem[] DirectoryShortcutCommands =>
        [
            new CommandItem(
                new AsyncRelayCommand(() => ProcessRunner.OpenFolderBrowser(Compat.AppDataHome)),
                Resources.Label_AppData
            ),
            new CommandItem(
                new AsyncRelayCommand(
                    () => ProcessRunner.OpenFolderBrowser(Compat.AppDataHome.JoinDir("Logs"))
                ),
                Resources.Label_Logs
            ),
            new CommandItem(
                new AsyncRelayCommand(() => ProcessRunner.OpenFolderBrowser(settingsManager.LibraryDir)),
                Resources.Label_DataDirectory
            ),
            new CommandItem(
                new AsyncRelayCommand(() => ProcessRunner.OpenFolderBrowser(settingsManager.ModelsDirectory)),
                Resources.Label_Checkpoints
            ),
            new CommandItem(
                new AsyncRelayCommand(
                    () => ProcessRunner.OpenFolderBrowser(settingsManager.LibraryDir.JoinDir("Packages"))
                ),
                Resources.Label_Packages
            )
        ];

    #endregion

    #region System

    /// <summary>
    /// Adds Stability Matrix to Start Menu for the current user.
    /// </summary>
    [RelayCommand]
    private async Task AddToStartMenu()
    {
        if (!Compat.IsWindows)
        {
            notificationService.Show("Not supported", "This feature is only supported on Windows.");
            return;
        }

        await using var _ = new MinimumDelay(200, 300);

        var shortcutDir = new DirectoryPath(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs"
        );
        var shortcutLink = shortcutDir.JoinFile("Stability Matrix.lnk");

        var appPath = Compat.AppCurrentPath;
        var iconPath = shortcutDir.JoinFile("Stability Matrix.ico");
        await Assets.AppIcon.ExtractTo(iconPath);

        WindowsShortcuts.CreateShortcut(shortcutLink, appPath, iconPath, "Stability Matrix");

        notificationService.Show(
            "Added to Start Menu",
            "Stability Matrix has been added to the Start Menu.",
            NotificationType.Success
        );
    }

    /// <summary>
    /// Add Stability Matrix to Start Menu for all users.
    /// <remarks>Requires Admin elevation.</remarks>
    /// </summary>
    [RelayCommand]
    private async Task AddToGlobalStartMenu()
    {
        if (!Compat.IsWindows)
        {
            notificationService.Show("Not supported", "This feature is only supported on Windows.");
            return;
        }

        // Confirmation dialog
        var dialog = new BetterContentDialog
        {
            Title = "This will create a shortcut for Stability Matrix in the Start Menu for all users",
            Content = "You will be prompted for administrator privileges. Continue?",
            PrimaryButtonText = Resources.Action_Yes,
            CloseButtonText = Resources.Action_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await using var _ = new MinimumDelay(200, 300);

        var shortcutDir = new DirectoryPath(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs"
        );
        var shortcutLink = shortcutDir.JoinFile("Stability Matrix.lnk");

        var appPath = Compat.AppCurrentPath;
        var iconPath = shortcutDir.JoinFile("Stability Matrix.ico");

        // We can't directly write to the targets, so extract to temporary directory first
        using var tempDir = new TempDirectoryPath();

        await Assets.AppIcon.ExtractTo(tempDir.JoinFile("Stability Matrix.ico"));
        WindowsShortcuts.CreateShortcut(
            tempDir.JoinFile("Stability Matrix.lnk"),
            appPath,
            iconPath,
            "Stability Matrix"
        );

        // Move to target
        try
        {
            var moveLinkResult = await WindowsElevated.MoveFiles(
                (tempDir.JoinFile("Stability Matrix.lnk"), shortcutLink),
                (tempDir.JoinFile("Stability Matrix.ico"), iconPath)
            );
            if (moveLinkResult != 0)
            {
                notificationService.ShowPersistent(
                    "Failed to create shortcut",
                    $"Could not copy shortcut",
                    NotificationType.Error
                );
            }
        }
        catch (Win32Exception e)
        {
            // We'll get this exception if user cancels UAC
            Logger.Warn(e, "Could not create shortcut");
            notificationService.Show("Could not create shortcut", "", NotificationType.Warning);
            return;
        }

        notificationService.Show(
            "Added to Start Menu",
            "Stability Matrix has been added to the Start Menu for all users.",
            NotificationType.Success
        );
    }

    public async Task PickNewDataDirectory()
    {
        var viewModel = dialogFactory.Get<SelectDataDirectoryViewModel>();
        var dialog = new BetterContentDialog
        {
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            IsFooterVisible = false,
            Content = new SelectDataDirectoryDialog { DataContext = viewModel }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // 1. For portable mode, call settings.SetPortableMode()
            if (viewModel.IsPortableMode)
            {
                settingsManager.SetPortableMode();
            }
            // 2. For custom path, call settings.SetLibraryPath(path)
            else
            {
                settingsManager.SetLibraryPath(viewModel.DataDirectory);
            }

            // Restart
            var restartDialog = new BetterContentDialog
            {
                Title = "Restart required",
                Content = "Stability Matrix must be restarted for the changes to take effect.",
                PrimaryButtonText = Resources.Action_Restart,
                DefaultButton = ContentDialogButton.Primary,
                IsSecondaryButtonEnabled = false,
            };
            await restartDialog.ShowAsync();

            Process.Start(Compat.AppCurrentPath);
            App.Shutdown();
        }
    }

    #endregion

    #region Debug Section
    public void LoadDebugInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        DebugPaths = $"""
                      Current Working Directory [Environment.CurrentDirectory]
                        "{Environment.CurrentDirectory}"
                      App Directory [Assembly.GetExecutingAssembly().Location]
                        "{assembly.Location}"
                      App Directory [AppContext.BaseDirectory]
                        "{AppContext.BaseDirectory}"
                      AppData Directory [SpecialFolder.ApplicationData]
                        "{appData}"
                      """;

        // 1. Check portable mode
        var appDir = Compat.AppCurrentDir;
        var expectedPortableFile = Path.Combine(appDir, "Data", ".sm-portable");
        var isPortableMode = File.Exists(expectedPortableFile);

        DebugCompatInfo = $"""
                            Platform: {Compat.Platform}
                            AppData: {Compat.AppData}
                            AppDataHome: {Compat.AppDataHome}
                            AppCurrentDir: {Compat.AppCurrentDir}
                            ExecutableName: {Compat.GetExecutableName()}
                            AppName: {Compat.GetAppName()}
                            -- Settings --
                            Expected Portable Marker file: {expectedPortableFile}
                            Portable Marker file exists: {isPortableMode}
                            IsLibraryDirSet = {settingsManager.IsLibraryDirSet}
                            IsPortableMode = {settingsManager.IsPortableMode}
                            """;

        // Get Gpu info
        var gpuInfo = "";
        foreach (var (i, gpu) in HardwareHelper.IterGpuInfo().Enumerate())
        {
            gpuInfo += $"[{i + 1}] {gpu}\n";
        }
        DebugGpuInfo = gpuInfo;
    }

    // Debug buttons
    [RelayCommand]
    private void DebugNotification()
    {
        notificationService.Show(
            new Notification(
                title: "Test Notification",
                message: "Here is some message",
                type: NotificationType.Information
            )
        );
    }

    [RelayCommand]
    private async Task DebugContentDialog()
    {
        var dialog = new ContentDialog
        {
            DefaultButton = ContentDialogButton.Primary,
            Title = "Test title",
            PrimaryButtonText = Resources.Action_OK,
            CloseButtonText = Resources.Action_Close
        };

        var result = await dialog.ShowAsync();
        notificationService.Show(new Notification("Content dialog closed", $"Result: {result}"));
    }

    [RelayCommand]
    private void DebugThrowException()
    {
        throw new OperationCanceledException("Example Message");
    }

    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task DebugThrowAsyncException()
    {
        await Task.Yield();

        throw new ApplicationException("Example Message");
    }

    [RelayCommand]
    private async Task DebugMakeImageGrid()
    {
        var provider = App.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions() { AllowMultiple = true });

        if (files.Count == 0)
            return;

        var images = await files.SelectAsync(async f => SKImage.FromEncodedData(await f.OpenReadAsync()));

        var grid = ImageProcessor.CreateImageGrid(images.ToImmutableArray());

        // Show preview

        using var peekPixels = grid.PeekPixels();
        using var data = peekPixels.Encode(SKEncodedImageFormat.Jpeg, 100);
        await using var stream = data.AsStream();

        var bitmap = WriteableBitmap.Decode(stream);

        var galleryImages = new List<ImageSource> { new(bitmap), };
        galleryImages.AddRange(files.Select(f => new ImageSource(f.Path.ToString())));

        var imageBox = new ImageGalleryCard
        {
            Width = 1000,
            Height = 900,
            DataContext = dialogFactory.Get<ImageGalleryCardViewModel>(vm =>
            {
                vm.ImageSources.AddRange(galleryImages);
            })
        };

        var dialog = new BetterContentDialog
        {
            MaxDialogWidth = 1000,
            MaxDialogHeight = 1000,
            FullSizeDesired = true,
            Content = imageBox,
            CloseButtonText = "Close",
            ContentVerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task DebugLoadCompletionCsv()
    {
        var provider = App.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions());

        if (files.Count == 0)
            return;

        await completionProvider.LoadFromFile(files[0].TryGetLocalPath()!, true);

        notificationService.Show("Loaded completion file", "");
    }

    [RelayCommand]
    private async Task DebugImageMetadata()
    {
        var provider = App.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions());

        if (files.Count == 0)
            return;

        var metadata = ImageMetadata.ParseFile(files[0].TryGetLocalPath()!);
        var textualTags = metadata.GetTextualData()?.ToArray();

        if (textualTags is null)
        {
            notificationService.Show("No textual data found", "");
            return;
        }

        if (metadata.GetGenerationParameters() is { } parameters)
        {
            var parametersJson = JsonSerializer.Serialize(parameters);
            var dialog = DialogHelper.CreateJsonDialog(parametersJson, "Generation Parameters");
            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    private async Task DebugRefreshModelsIndex()
    {
        await modelIndexService.RefreshIndex();
    }

    [RelayCommand]
    private async Task DebugTrackedDownload()
    {
        var textFields = new TextBoxField[]
        {
            new() { Label = "Url", },
            new() { Label = "File path" }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Add download", "", textFields);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var url = textFields[0].Text;
            var filePath = textFields[1].Text;
            var download = trackedDownloadService.NewDownload(new Uri(url), new FilePath(filePath));
            download.Start();
        }
    }
    #endregion

    #region Debug Commands

    public CommandItem[] DebugCommands =>
        [
            new CommandItem(DebugRefreshModelIndexCommand),
            new CommandItem(DebugFindLocalModelFromIndexCommand),
            new CommandItem(DebugExtractDmgCommand),
            new CommandItem(DebugShowNativeNotificationCommand),
            new CommandItem(DebugClearImageCacheCommand),
            new CommandItem(DebugGCCollectCommand),
            new CommandItem(DebugExtractImagePromptsToTxtCommand),
            new CommandItem(DebugShowImageMaskEditorCommand),
            new CommandItem(DebugExtractImagePromptsToTxtCommand),
            new CommandItem(DebugShowConfirmDeleteDialogCommand),
            new CommandItem(DebugShowModelMetadataEditorDialogCommand),
        ];

    [RelayCommand]
    private async Task DebugShowModelMetadataEditorDialog()
    {
        var vm = dialogFactory.Get<ModelMetadataEditorDialogViewModel>();
        vm.ThumbnailFilePath = Assets.NoImage.ToString();
        vm.Tags = "tag1, tag2, tag3";
        vm.ModelDescription = "This is a description";
        vm.ModelName = "Model Name";
        vm.VersionName = "1.0.0";
        vm.TrainedWords = "word1, word2, word3";
        vm.ModelType = CivitModelType.Checkpoint;
        vm.BaseModelType = CivitBaseModelType.Pony;

        var dialog = vm.GetDialog();
        dialog.MinDialogHeight = 800;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.IsFooterVisible = true;
        dialog.PrimaryButtonText = "Save";
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.CloseButtonText = "Cancel";

        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task DebugShowConfirmDeleteDialog()
    {
        var vm = dialogFactory.Get<ConfirmDeleteDialogViewModel>();

        vm.IsRecycleBinAvailable = false;
        vm.PathsToDelete = Enumerable
            .Range(1, 64)
            .Select(i => $"C:/Users/ExampleUser/Data/ExampleFile{i}.txt")
            .ToArray();

        await vm.GetDialog().ShowAsync();
    }

    [RelayCommand]
    private async Task DebugRefreshModelIndex()
    {
        await modelIndexService.RefreshIndex();
    }

    [RelayCommand]
    private async Task DebugFindLocalModelFromIndex()
    {
        var textFields = new TextBoxField[]
        {
            new() { Label = "Blake3 Hash" },
            new() { Label = "SharedFolderType" }
        };

        var dialog = DialogHelper.CreateTextEntryDialog("Find Local Model", "", textFields);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var timer = new Stopwatch();
            List<LocalModelFile> results;

            if (textFields.ElementAtOrDefault(0)?.Text is { } hash && !string.IsNullOrWhiteSpace(hash))
            {
                timer.Restart();
                results = (await modelIndexService.FindByHashAsync(hash)).ToList();
                timer.Stop();
            }
            else if (textFields.ElementAtOrDefault(1)?.Text is { } type && !string.IsNullOrWhiteSpace(type))
            {
                var folderTypes = Enum.Parse<SharedFolderType>(type, true);
                timer.Restart();
                results = (await modelIndexService.FindByModelTypeAsync(folderTypes)).ToList();
                timer.Stop();
            }
            else
            {
                return;
            }

            if (results.Count != 0)
            {
                await DialogHelper
                    .CreateMarkdownDialog(
                        string.Join(
                            "\n\n",
                            results.Select(
                                (model, i) =>
                                    $"[{i + 1}] {model.RelativePath.ToRepr()} "
                                    + $"({model.DisplayModelName}, {model.DisplayModelVersion})"
                            )
                        ),
                        $"Found Models ({CodeTimer.FormatTime(timer.Elapsed)})"
                    )
                    .ShowAsync();
            }
            else
            {
                await DialogHelper
                    .CreateMarkdownDialog(":(", $"No models found ({CodeTimer.FormatTime(timer.Elapsed)})")
                    .ShowAsync();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(IsMacOS))]
    private async Task DebugExtractDmg()
    {
        if (!Compat.IsMacOS)
            return;

        // Select File
        var files = await App.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Select .dmg file" }
        );
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } dmgFile)
            return;

        // Select output directory
        var folders = await App.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Select output directory" }
        );
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } outputDir)
            return;

        // Extract
        notificationService.Show("Extracting...", dmgFile);

        await ArchiveHelper.ExtractDmg(dmgFile, outputDir);

        notificationService.Show("Extraction Complete", dmgFile);
    }

    [RelayCommand]
    private async Task DebugShowNativeNotification()
    {
        var nativeManager = await notificationService.GetNativeNotificationManagerAsync();

        if (nativeManager is null)
        {
            notificationService.Show(
                "Not supported",
                "Native notifications are not supported on this platform.",
                NotificationType.Warning
            );
            return;
        }

        await nativeManager.ShowNotification(
            new DesktopNotifications.Notification
            {
                Title = "Test Notification",
                Body = "Here is some message",
                Buttons = { ("Action", "__Debug_Action"), ("Close", "__Debug_Close"), }
            }
        );
    }

    [RelayCommand]
    private void DebugClearImageCache()
    {
        if (ImageLoader.AsyncImageLoader is FallbackRamCachedWebImageLoader loader)
        {
            loader.ClearCache();
        }
    }

    [RelayCommand]
    private void DebugGCCollect()
    {
        GC.Collect();
    }

    [RelayCommand]
    private async Task DebugExtractImagePromptsToTxt()
    {
        // Choose images
        var provider = App.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true });

        if (files.Count == 0)
            return;

        var images = await Task.Run(
            () => files.Select(f => LocalImageFile.FromPath(f.TryGetLocalPath()!)).ToList()
        );

        var successfulFiles = new List<LocalImageFile>();

        foreach (var localImage in images)
        {
            var imageFile = new FilePath(localImage.AbsolutePath);

            // Write a txt with the same name as the image
            var txtFile = imageFile.WithName(imageFile.NameWithoutExtension + ".txt");

            // Read metadata
            if (localImage.GenerationParameters?.PositivePrompt is { } positivePrompt)
            {
                await File.WriteAllTextAsync(txtFile, positivePrompt);

                successfulFiles.Add(localImage);
            }
        }

        notificationService.Show(
            "Extracted prompts",
            $"Extracted prompts from {successfulFiles.Count}/{images.Count} images.",
            NotificationType.Success
        );
    }

    [RelayCommand]
    private async Task DebugShowImageMaskEditor()
    {
        // Choose background image
        var provider = App.StorageProvider;
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions());

        if (files.Count == 0)
            return;

        var bitmap = await Task.Run(() => SKBitmap.Decode(files[0].TryGetLocalPath()!));

        var vm = dialogFactory.Get<MaskEditorViewModel>();

        vm.PaintCanvasViewModel.BackgroundImage = bitmap;

        await vm.GetDialog().ShowAsync();
    }

    #endregion

    #region Info Section

    public void OnVersionClick()
    {
        // Ignore if already enabled
        if (SharedState.IsDebugMode)
            return;

        VersionTapCount++;

        switch (VersionTapCount)
        {
            // Reached required threshold
            case >= VersionTapCountThreshold:
            {
                IsVersionTapTeachingTipOpen = false;
                // Enable debug options
                SharedState.IsDebugMode = true;
                notificationService.Show(
                    "Debug options enabled",
                    "Warning: Improper use may corrupt application state or cause loss of data."
                );
                VersionTapCount = 0;
                break;
            }
            // Open teaching tip above 3rd click
            case >= 3:
                IsVersionTapTeachingTipOpen = true;
                break;
        }
    }

    [RelayCommand]
    private async Task ShowLicensesDialog()
    {
        try
        {
            var markdown = GetLicensesMarkdown();

            var dialog = DialogHelper.CreateMarkdownDialog(markdown, "Licenses");
            dialog.MaxDialogHeight = 600;
            await dialog.ShowAsync();
        }
        catch (Exception e)
        {
            notificationService.Show("Failed to read licenses information", $"{e}", NotificationType.Error);
        }
    }

    private static string GetLicensesMarkdown()
    {
        // Read licenses.json
        using var reader = new StreamReader(Assets.LicensesJson.Open());
        var licenses =
            JsonSerializer.Deserialize<IReadOnlyList<LicenseInfo>>(reader.ReadToEnd())
            ?? throw new InvalidOperationException("Failed to read licenses.json");

        // Generate markdown
        var builder = new StringBuilder();
        foreach (var license in licenses)
        {
            builder.AppendLine(
                $"## [{license.PackageName}]({license.PackageUrl}) by {string.Join(", ", license.Authors)}"
            );
            builder.AppendLine();
            builder.AppendLine(license.Description);
            builder.AppendLine();
            builder.AppendLine($"[{license.LicenseUrl}]({license.LicenseUrl})");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    #endregion
}
