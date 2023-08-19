using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using NLog;
using SkiaSharp;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.TagCompletion;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
public partial class SettingsViewModel : PageViewModelBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private readonly IPyRunner pyRunner;
    private readonly ServiceManager<ViewModelBase> dialogFactory;
    private readonly ICompletionProvider completionProvider;
    
    public SharedState SharedState { get; }
    
    public override string Title => "Settings";
    public override IconSource IconSource => new SymbolIconSource {Symbol = Symbol.Settings, IsFilled = true};
    
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public string AppVersion => $"Version {Compat.AppVersion}" + 
                                (Program.IsDebugBuild ? " (Debug)" : "");
    
    // Theme section
    [ObservableProperty] private string? selectedTheme;
    
    public IReadOnlyList<string> AvailableThemes { get; } = new[]
    {
        "Light",
        "Dark",
        "System",
    };

    public IReadOnlyList<float> AnimationScaleOptions { get; } = new[]
    {
        0f,
        0.25f,
        0.5f,
        0.75f,
        1f,
        1.25f,
        1.5f,
        1.75f,
        2f,
    };
    
    [ObservableProperty] private float selectedAnimationScale;
    
    // Shared folder options
    [ObservableProperty] private bool removeSymlinksOnShutdown;
    
    // Inference UI section
    [ObservableProperty] private bool isPromptCompletionEnabled;
    [ObservableProperty]
    private IReadOnlyList<string> availableTagCompletionCsvs = Array.Empty<string>();
    [ObservableProperty]
    private string? selectedTagCompletionCsv;
    [ObservableProperty]
    private bool isCompletionRemoveUnderscoresEnabled;
    
    // Integrations section
    [ObservableProperty] private bool isDiscordRichPresenceEnabled;
    
    // Debug section
    [ObservableProperty] private string? debugPaths;
    [ObservableProperty] private string? debugCompatInfo;
    [ObservableProperty] private string? debugGpuInfo;
    
    // Info section
    private const int VersionTapCountThreshold = 7;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(VersionFlyoutText))] private int versionTapCount;
    [ObservableProperty] private bool isVersionTapTeachingTipOpen;
    public string VersionFlyoutText => $"You are {VersionTapCountThreshold - VersionTapCount} clicks away from enabling Debug options.";
    
    public SettingsViewModel(
        INotificationService notificationService, 
        ISettingsManager settingsManager,
        IPrerequisiteHelper prerequisiteHelper,
        IPyRunner pyRunner,
        ServiceManager<ViewModelBase> dialogFactory,
        SharedState sharedState, 
        ICompletionProvider completionProvider)
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;
        this.prerequisiteHelper = prerequisiteHelper;
        this.pyRunner = pyRunner;
        this.dialogFactory = dialogFactory;
        this.completionProvider = completionProvider;
        
        SharedState = sharedState;
        
        SelectedTheme = settingsManager.Settings.Theme ?? AvailableThemes[1];
        RemoveSymlinksOnShutdown = settingsManager.Settings.RemoveFolderLinksOnShutdown;
        SelectedAnimationScale = settingsManager.Settings.AnimationScale;
        
        settingsManager.RelayPropertyFor(this, 
            vm => vm.SelectedTheme, 
            settings => settings.Theme);
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.IsDiscordRichPresenceEnabled,
            settings => settings.IsDiscordRichPresenceEnabled,
            true);
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.SelectedAnimationScale,
            settings => settings.AnimationScale);
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.SelectedTagCompletionCsv,
            settings => settings.TagCompletionCsv);
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.IsPromptCompletionEnabled,
            settings => settings.IsPromptCompletionEnabled,
            true);
        
        settingsManager.RelayPropertyFor(this,
            vm => vm.IsCompletionRemoveUnderscoresEnabled,
            settings => settings.IsCompletionRemoveUnderscoresEnabled,
            true);
        
        DebugThrowAsyncExceptionCommand.WithNotificationErrorHandler(notificationService, LogLevel.Warn);
        ImportTagCsvCommand.WithNotificationErrorHandler(notificationService, LogLevel.Warn);
    }

    /// <inheritdoc />
    public override void OnLoaded()
    {
        base.OnLoaded();

        UpdateAvailableTagCompletionCsvs();
    }

    partial void OnSelectedThemeChanged(string? value)
    {
        // In case design / tests
        if (Application.Current is null) return;
        // Change theme
        Application.Current.RequestedThemeVariant = value switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
    
    partial void OnRemoveSymlinksOnShutdownChanged(bool value)
    {
        settingsManager.Transaction(s => s.RemoveFolderLinksOnShutdown = value);
    }

    public async Task ResetCheckpointCache()
    {
        settingsManager.Transaction(s => s.InstalledModelHashes = new HashSet<string>());
        await Task.Run(() => settingsManager.IndexCheckpoints());
        notificationService.Show("Checkpoint cache reset", "The checkpoint cache has been reset.",
            NotificationType.Success);
    }

    #region Package Environment
    
    [RelayCommand]
    private async Task OpenEnvVarsDialog()
    {
        var viewModel = dialogFactory.Get<EnvVarsViewModel>();
        
        // Load current settings
        var current = settingsManager.Settings.EnvironmentVariables 
                      ?? new Dictionary<string, string>();
        viewModel.EnvVars = new ObservableCollection<EnvVarKeyPair>(
            current.Select(kvp => new EnvVarKeyPair(kvp.Key, kvp.Value)));
        
        var dialog = new BetterContentDialog
        {
            Content = new EnvVarsDialog
            {
                DataContext = viewModel
            },
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Cancel",
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
            Title = "Python version info",
            Content = result,
            PrimaryButtonText = "Ok",
            IsPrimaryButtonEnabled = true
        };
        dialog.Title = "Python version info";
        dialog.Content = result;
        dialog.PrimaryButtonText = "Ok";
        await dialog.ShowAsync();
    }
    
    #endregion

    #region Inference UI
    
    private void UpdateAvailableTagCompletionCsvs()
    {
        if (!settingsManager.IsLibraryDirSet) return;
        
        var tagsDir = settingsManager.TagsDirectory;
        if (!tagsDir.Exists) return;
        
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
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            FileTypeFilter =  new List<FilePickerFileType>
            {
                new("CSV")
                {
                    Patterns = new[] {"*.csv"},
                }
            }
        });
        
        if (files.Count == 0) return;

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
        
        notificationService.Show($"Imported {sourceFile.Name}", 
            $"The {sourceFile.Name} file has been imported.", NotificationType.Success);
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
            notificationService.Show(
                "Not supported", "This feature is only supported on Windows.");
            return;
        }
        
        await using var _ = new MinimumDelay(200, 300);
        
        var shortcutDir = new DirectoryPath(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");
        var shortcutLink = shortcutDir.JoinFile("Stability Matrix.lnk");

        var appPath = Compat.AppCurrentPath;
        var iconPath = shortcutDir.JoinFile("Stability Matrix.ico");
        await Assets.AppIcon.ExtractTo(iconPath);
        
        WindowsShortcuts.CreateShortcut(
            shortcutLink, appPath, iconPath, "Stability Matrix");
        
        notificationService.Show("Added to Start Menu",
            "Stability Matrix has been added to the Start Menu.", NotificationType.Success);
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
            notificationService.Show(
                "Not supported", "This feature is only supported on Windows.");
            return;
        }
        
        // Confirmation dialog
        var dialog = new BetterContentDialog
        {
            Title = "This will create a shortcut for Stability Matrix in the Start Menu for all users",
            Content = "You will be prompted for administrator privileges. Continue?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }
        
        await using var _ = new MinimumDelay(200, 300);
        
        var shortcutDir = new DirectoryPath(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            "Programs");
        var shortcutLink = shortcutDir.JoinFile("Stability Matrix.lnk");
        
        var appPath = Compat.AppCurrentPath;
        var iconPath = shortcutDir.JoinFile("Stability Matrix.ico");
        
        // We can't directly write to the targets, so extract to temporary directory first
        using var tempDir = new TempDirectoryPath();
        
        await Assets.AppIcon.ExtractTo(tempDir.JoinFile("Stability Matrix.ico"));
        WindowsShortcuts.CreateShortcut(
            tempDir.JoinFile("Stability Matrix.lnk"), appPath, iconPath, 
            "Stability Matrix");
        
        // Move to target
        try
        {
            var moveLinkResult = await WindowsElevated.MoveFiles(
                (tempDir.JoinFile("Stability Matrix.lnk"), shortcutLink),
                (tempDir.JoinFile("Stability Matrix.ico"), iconPath));
            if (moveLinkResult != 0)
            {
                notificationService.ShowPersistent("Failed to create shortcut", $"Could not copy shortcut", 
                    NotificationType.Error);
            }
        }
        catch (Win32Exception e)
        {
            // We'll get this exception if user cancels UAC
            Logger.Warn(e, "Could not create shortcut");
            notificationService.Show("Could not create shortcut", "", NotificationType.Warning);
            return;
        }
        
        notificationService.Show("Added to Start Menu", 
            "Stability Matrix has been added to the Start Menu for all users.", NotificationType.Success);
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
            gpuInfo += $"[{i+1}] {gpu}\n";
        }
        DebugGpuInfo = gpuInfo;
    }
    
    // Debug buttons
    [RelayCommand]
    private void DebugNotification()
    {
        notificationService.Show(new Notification(
            title: "Test Notification",
            message: "Here is some message",
            type: NotificationType.Information));
    }

    [RelayCommand]
    private async Task DebugContentDialog()
    {
        var dialog = new ContentDialog
        {
            DefaultButton = ContentDialogButton.Primary,
            Title = "Test title",
            PrimaryButtonText = "OK",
            CloseButtonText = "Close"
        };

        var result = await dialog.ShowAsync();
        notificationService.Show(new Notification("Content dialog closed",
            $"Result: {result}"));
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
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = true
        });
        
        if (files.Count == 0) return;
        
        var images = await files.SelectAsync(async f =>
            SKImage.FromEncodedData(await f.OpenReadAsync()));

        var grid = ImageProcessor.CreateImageGrid(images.ToImmutableArray());
        
        // Show preview

        using var peekPixels = grid.PeekPixels();
        using var data = peekPixels.Encode(SKEncodedImageFormat.Jpeg, 100);
        await using var stream = data.AsStream();

        var image = new AdvancedImageBox
        {
            Image = WriteableBitmap.Decode(stream),
            Width = 600,
            Height = 800,
            PixelGridZoomThreshold = 10,
            ConstrainZoomOutToFitLevel = true,
            MaxZoom = 6400 * 2,
        };

        var dialog = new BetterContentDialog
        {
            Content = image,
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
        
        if (files.Count == 0) return;
        
        await completionProvider.LoadFromFile(files[0].TryGetLocalPath()!, true);
        
        notificationService.Show("Loaded completion file", "");
    }
    #endregion

    #region Info Section

    public void OnVersionClick()
    {
        // Ignore if already enabled
        if (SharedState.IsDebugMode) return;
        
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
                    "Debug options enabled", "Warning: Improper use may corrupt application state or cause loss of data.");
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
            notificationService.Show("Failed to read licenses information", 
                $"{e}", NotificationType.Error);
        }
    }

    private static string GetLicensesMarkdown()
    {
        // Read licenses.json
        using var reader = new StreamReader(Assets.LicensesJson.Open());
        var licenses = JsonSerializer
            .Deserialize<IReadOnlyList<LicenseInfo>>(reader.ReadToEnd()) ??
                       throw new InvalidOperationException("Failed to read licenses.json");
        
        // Generate markdown
        var builder = new StringBuilder();
        foreach (var license in licenses)
        {
            builder.AppendLine($"## [{license.PackageName}]({license.PackageUrl}) by {string.Join(", ", license.Authors)}");
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
