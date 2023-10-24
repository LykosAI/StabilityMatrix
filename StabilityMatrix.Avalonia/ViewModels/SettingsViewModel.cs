using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Inference;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
[Singleton]
public partial class SettingsViewModel : PageViewModelBase
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

    public SharedState SharedState { get; }

    public override string Title => "Settings";
    public override IconSource IconSource =>
        new SymbolIconSource { Symbol = Symbol.Settings, IsFilled = true };

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public string AppVersion =>
        $"Version {Compat.AppVersion}" + (Program.IsDebugBuild ? " (Debug)" : "");

    // Theme section
    [ObservableProperty]
    private string? selectedTheme;

    public IReadOnlyList<string> AvailableThemes { get; } = new[] { "Light", "Dark", "System", };

    [ObservableProperty]
    private CultureInfo selectedLanguage;

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public IReadOnlyList<CultureInfo> AvailableLanguages => Cultures.SupportedCultures;

    public IReadOnlyList<float> AnimationScaleOptions { get; } =
        new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, };

    [ObservableProperty]
    private float selectedAnimationScale;

    // Shared folder options
    [ObservableProperty]
    private bool removeSymlinksOnShutdown;

    // Inference UI section
    [ObservableProperty]
    private bool isPromptCompletionEnabled = true;

    [ObservableProperty]
    private IReadOnlyList<string> availableTagCompletionCsvs = Array.Empty<string>();

    [ObservableProperty]
    private string? selectedTagCompletionCsv;

    [ObservableProperty]
    private bool isCompletionRemoveUnderscoresEnabled = true;

    [ObservableProperty]
    [CustomValidation(typeof(SettingsViewModel), nameof(ValidateOutputImageFileNameFormat))]
    private string? outputImageFileNameFormat;

    [ObservableProperty]
    private string? outputImageFileNameFormatSample;

    public IEnumerable<FileNameFormatVar> OutputImageFileNameFormatVars =>
        FileNameFormatProvider
            .GetSample()
            .Substitutions.Select(
                kv =>
                    new FileNameFormatVar
                    {
                        Variable = $"{{{kv.Key}}}",
                        Example = kv.Value.Invoke()
                    }
            );

    [ObservableProperty]
    private bool isImageViewerPixelGridEnabled = true;

    // Integrations section
    [ObservableProperty]
    private bool isDiscordRichPresenceEnabled;

    // Debug section
    [ObservableProperty]
    private string? debugPaths;

    [ObservableProperty]
    private string? debugCompatInfo;

    [ObservableProperty]
    private string? debugGpuInfo;

    // Info section
    private const int VersionTapCountThreshold = 7;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(VersionFlyoutText))]
    private int versionTapCount;

    [ObservableProperty]
    private bool isVersionTapTeachingTipOpen;
    public string VersionFlyoutText =>
        $"You are {VersionTapCountThreshold - VersionTapCount} clicks away from enabling Debug options.";

    public string DataDirectory =>
        settingsManager.IsLibraryDirSet ? settingsManager.LibraryDir : "Not set";

    public SettingsViewModel(
        INotificationService notificationService,
        ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner,
        ServiceManager<ViewModelBase> dialogFactory,
        ITrackedDownloadService trackedDownloadService,
        SharedState sharedState,
        ICompletionProvider completionProvider,
        IModelIndexService modelIndexService
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

        SharedState = sharedState;

        SelectedTheme = settingsManager.Settings.Theme ?? AvailableThemes[1];
        SelectedLanguage = Cultures.GetSupportedCultureOrDefault(settingsManager.Settings.Language);
        RemoveSymlinksOnShutdown = settingsManager.Settings.RemoveFolderLinksOnShutdown;
        SelectedAnimationScale = settingsManager.Settings.AnimationScale;

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
            vm => vm.SelectedTagCompletionCsv,
            settings => settings.TagCompletionCsv
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsPromptCompletionEnabled,
            settings => settings.IsPromptCompletionEnabled,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsCompletionRemoveUnderscoresEnabled,
            settings => settings.IsCompletionRemoveUnderscoresEnabled,
            true
        );

        this.WhenPropertyChanged(vm => vm.OutputImageFileNameFormat)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Subscribe(formatProperty =>
            {
                var provider = FileNameFormatProvider.GetSample();
                var template = formatProperty.Value ?? string.Empty;

                if (
                    !string.IsNullOrEmpty(template)
                    && provider.Validate(template) == ValidationResult.Success
                )
                {
                    var format = FileNameFormat.Parse(template, provider);
                    OutputImageFileNameFormatSample = format.GetFileName() + ".png";
                }
                else
                {
                    // Use default format if empty
                    var defaultFormat = FileNameFormat.Parse(
                        FileNameFormat.DefaultTemplate,
                        provider
                    );
                    OutputImageFileNameFormatSample = defaultFormat.GetFileName() + ".png";
                }
            });

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.OutputImageFileNameFormat,
            settings => settings.InferenceOutputImageFileNameFormat,
            true
        );

        settingsManager.RelayPropertyFor(
            this,
            vm => vm.IsImageViewerPixelGridEnabled,
            settings => settings.IsImageViewerPixelGridEnabled,
            true
        );

        DebugThrowAsyncExceptionCommand.WithNotificationErrorHandler(
            notificationService,
            LogLevel.Warn
        );
        ImportTagCsvCommand.WithNotificationErrorHandler(notificationService, LogLevel.Warn);
        DebugInferenceUploadImageCommand.WithNotificationErrorHandler(
            notificationService,
            LogLevel.Warn
        );
    }

    /// <inheritdoc />
    public override async Task OnLoadedAsync()
    {
        await base.OnLoadedAsync();

        await notificationService.TryAsync(completionProvider.Setup());

        UpdateAvailableTagCompletionCsvs();
    }

    public static ValidationResult ValidateOutputImageFileNameFormat(
        string? format,
        ValidationContext context
    )
    {
        return FileNameFormatProvider.GetSample().Validate(format ?? string.Empty);
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

            Cultures.TrySetSupportedCulture(newValue);
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
                    Process.Start(Compat.AppCurrentPath);
                    App.Shutdown();
                }
            });
        }
        else
        {
            Logger.Info(
                "Requested invalid language change from {Old} to {New}",
                oldValue,
                newValue
            );
        }
    }

    partial void OnRemoveSymlinksOnShutdownChanged(bool value)
    {
        settingsManager.Transaction(s => s.RemoveFolderLinksOnShutdown = value);
    }

    public async Task ResetCheckpointCache()
    {
        settingsManager.Transaction(s => s.InstalledModelHashes = new HashSet<string>());
        await Task.Run(() => settingsManager.IndexCheckpoints());
        notificationService.Show(
            "Checkpoint cache reset",
            "The checkpoint cache has been reset.",
            NotificationType.Success
        );
    }

    #region Package Environment

    [RelayCommand]
    private async Task OpenEnvVarsDialog()
    {
        var viewModel = dialogFactory.Get<EnvVarsViewModel>();

        // Load current settings
        var current =
            settingsManager.Settings.EnvironmentVariables ?? new Dictionary<string, string>();
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
            var newEnvVars = viewModel.EnvVars
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                .GroupBy(kvp => kvp.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
            settingsManager.Transaction(s => s.EnvironmentVariables = newEnvVars);
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

    #endregion

    #region Inference UI

    private void UpdateAvailableTagCompletionCsvs()
    {
        if (!settingsManager.IsLibraryDirSet)
            return;

        var tagsDir = settingsManager.TagsDirectory;
        if (!tagsDir.Exists)
            return;

        var csvFiles = tagsDir.Info.EnumerateFiles("*.csv");
        AvailableTagCompletionCsvs = csvFiles.Select(f => f.Name).ToImmutableArray();

        // Set selected to current if exists
        var settingsCsv = settingsManager.Settings.TagCompletionCsv;
        if (settingsCsv is not null && AvailableTagCompletionCsvs.Contains(settingsCsv))
        {
            SelectedTagCompletionCsv = settingsCsv;
        }
    }

    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task ImportTagCsv()
    {
        var storage = App.StorageProvider;
        var files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("CSV") { Patterns = new[] { "*.csv" }, }
                }
            }
        );

        if (files.Count == 0)
            return;

        var sourceFile = new FilePath(files[0].TryGetLocalPath()!);

        var tagsDir = settingsManager.TagsDirectory;
        tagsDir.Create();

        // Copy to tags directory
        var targetFile = tagsDir.JoinFile(sourceFile.Name);
        await sourceFile.CopyToAsync(targetFile);

        // Update index
        UpdateAvailableTagCompletionCsvs();

        // Trigger load
        completionProvider.BackgroundLoadFromFile(targetFile, true);

        notificationService.Show(
            $"Imported {sourceFile.Name}",
            $"The {sourceFile.Name} file has been imported.",
            NotificationType.Success
        );
    }
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
            Title =
                "This will create a shortcut for Stability Matrix in the Start Menu for all users",
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
        var files = await provider.OpenFilePickerAsync(
            new FilePickerOpenOptions() { AllowMultiple = true }
        );

        if (files.Count == 0)
            return;

        var images = await files.SelectAsync(
            async f => SKImage.FromEncodedData(await f.OpenReadAsync())
        );

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

    [RelayCommand(FlowExceptionsToTaskScheduler = true)]
    private async Task DebugInferenceUploadImage()
    {
        if (
            await App.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions())
            is not { Count: > 0 } files
        )
            return;

        var file = files[0];

        if (
            App.Services.GetRequiredService<IInferenceClientManager>().Client is not { } comfyClient
        )
            return;

        await using var stream = await file.OpenReadAsync();

        var response = await comfyClient.UploadImageAsync(stream, "test.jpg");

        notificationService.ShowPersistent(
            "Uploaded image",
            $"Name: {response.Name}\nType: {response.Type}\nSubfolder: {response.SubFolder}"
        );
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
            notificationService.Show(
                "Failed to read licenses information",
                $"{e}",
                NotificationType.Error
            );
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
