using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdXaml;
using Microsoft.Extensions.Logging;
using Ookii.Dialogs.Wpf;
using Polly.Timeout;
using Refit;
using StabilityMatrix.Core.Api;
using StabilityMatrix.Core.Database;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Factory;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Helper;
using Wpf.Ui.Appearance;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Window;
using EventManager = StabilityMatrix.Core.Helper.EventManager;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;

namespace StabilityMatrix.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILogger<SettingsViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IDialogFactory dialogFactory;
    private readonly IPackageFactory packageFactory;
    private readonly IPyRunner pyRunner;
    private readonly ISnackbarService snackbarService;
    private readonly ILiteDbContext liteDbContext;
    private readonly IPrerequisiteHelper prerequisiteHelper;
    private static string LicensesPath => "pack://application:,,,/Assets/licenses.json";
    public TextToFlowDocumentConverter? TextToFlowDocumentConverter { get; set; }

    public ObservableCollection<string> AvailableThemes => new()
    {
        "Light",
        "Dark",
        "System",
    };

    public ObservableCollection<WindowBackdropType> AvailableBackdrops => new()
    {
        WindowBackdropType.Mica,
        WindowBackdropType.Tabbed
    };
    private readonly IContentDialogService contentDialogService;
    private readonly IA3WebApiManager a3WebApiManager;

    [ObservableProperty] private bool isFileSearchFlyoutOpen;
    [ObservableProperty] private double fileSearchProgress;

    [ObservableProperty] private bool isPythonInstalling;

    [ObservableProperty] private string? webApiHost;
    [ObservableProperty] private string? webApiPort;
    [ObservableProperty] private string? webApiActivePackageHost;
    [ObservableProperty] private string? webApiActivePackagePort;
    
    partial void OnWebApiHostChanged(string? value)
    {
        settingsManager.Transaction(s => s.WebApiHost = value);
        a3WebApiManager.ResetClient();
    }
    
    partial void OnWebApiPortChanged(string? value)
    {
        settingsManager.Transaction(s => s.WebApiPort = value);
        a3WebApiManager.ResetClient();
    }
    
    [ObservableProperty] private bool keepFolderLinksOnShutdown;
    
    partial void OnKeepFolderLinksOnShutdownChanged(bool value)
    {
        if (value != settingsManager.Settings.RemoveFolderLinksOnShutdown)
        {
            settingsManager.Transaction(s => s.RemoveFolderLinksOnShutdown = value);
        }
    }

    public RefreshBadgeViewModel Text2ImageRefreshBadge { get; } = new()
    {
        SuccessToolTipText = "Connected",
        WorkingToolTipText = "Trying to connect...",
        FailToolTipText = "Failed to connect",
    };

    public SettingsViewModel(
        ISettingsManager settingsManager, 
        IContentDialogService contentDialogService,
        IDialogFactory dialogFactory,
        IA3WebApiManager a3WebApiManager, 
        IPyRunner pyRunner, 
        ISnackbarService snackbarService, 
        ILogger<SettingsViewModel> logger, 
        IPackageFactory packageFactory,
        ILiteDbContext liteDbContext,
        IPrerequisiteHelper prerequisiteHelper)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.contentDialogService = contentDialogService;
        this.dialogFactory = dialogFactory;
        this.snackbarService = snackbarService;
        this.a3WebApiManager = a3WebApiManager;
        this.pyRunner = pyRunner;
        this.liteDbContext = liteDbContext;
        this.prerequisiteHelper = prerequisiteHelper;
        SelectedTheme = settingsManager.Settings.Theme ?? "Dark";
        KeepFolderLinksOnShutdown = settingsManager.Settings.RemoveFolderLinksOnShutdown;
    }

    [ObservableProperty]
    private bool isDebugModeEnabled;
    partial void OnIsDebugModeEnabledChanged(bool value) => EventManager.Instance.OnDevModeSettingChanged(value);
    
    [ObservableProperty]
    private string selectedTheme;

    public string AppVersion => $"Version {Utilities.GetAppVersion()}";

    partial void OnSelectedThemeChanged(string value)
    {
        using var st = settingsManager.BeginTransaction();
        st.Settings.Theme = value;
        ApplyTheme(value);
    }

    [ObservableProperty]
    private string gpuInfo = $"{HardwareHelper.IterGpuInfo().FirstOrDefault()}";

    [ObservableProperty] private string? testProperty;

    [ObservableProperty] private bool isVersionFlyoutOpen;

    private const int AppVersionClickCountThreshold = 7;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionFlyoutText))]
    private int appVersionClickCount;
    
    public string VersionFlyoutText => $"You are {AppVersionClickCountThreshold - AppVersionClickCount} clicks away from enabling Debug options.";

    partial void OnIsVersionFlyoutOpenChanged(bool value)
    {
        // If set to false (from timeout) clear click count
        if (!value)
        {
            AppVersionClickCount = 0;
        }
    }

    public AsyncRelayCommand PythonVersionCommand => new(async () =>
    {
        // Ensure python installed
        if (!prerequisiteHelper.IsPythonInstalled)
        {
            IsPythonInstalling = true;
            // Need 7z as well for site packages repack
            await prerequisiteHelper.UnpackResourcesIfNecessary();
            await prerequisiteHelper.InstallPythonIfNecessary();
            IsPythonInstalling = false;
        }

        // Get python version
        await pyRunner.Initialize();
        var result = await pyRunner.GetVersionInfo();
        // Show dialog box
        var dialog = contentDialogService.CreateDialog();
        dialog.Title = "Python version info";
        dialog.Content = result;
        dialog.PrimaryButtonText = "Ok";
        await dialog.ShowAsync();
    });

    // Debug card commands
    public RelayCommand AddInstallationCommand => new(() =>
    {
        // Show dialog box to choose a folder
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.SelectedPath;
        if (path == null) return;

        // Create package
        var package = new InstalledPackage
        {
            Id = Guid.NewGuid(),
            DisplayName = Path.GetFileName(path),
#pragma warning disable CS0618 // Type or member is obsolete
            Path = path,
#pragma warning restore CS0618 // Type or member is obsolete
            PackageName = "dank-diffusion",
            PackageVersion = "v1.0.0",
        };

        // Add package to settings
        settingsManager.Transaction(s => s.InstalledPackages.Add(package));
    });

    // Debug card commands
    [RelayCommand]
    private async Task ModelFileSearchAsync()
    {
        // Show dialog box to choose a file
        var fileDialog = new VistaOpenFileDialog
        {
            CheckFileExists = true,
        };
        if (fileDialog.ShowDialog() != true) return;
        var path = fileDialog.FileName;
        // Hash file
        var timer = Stopwatch.StartNew();
        IsFileSearchFlyoutOpen = true;
        var progress = new Progress<ProgressReport>(report => FileSearchProgress = report.Percentage);
        var fileHash = await FileHash.GetBlake3Async(path, progress);
        var timeTakenHash = timer.Elapsed.TotalSeconds;
        IsFileSearchFlyoutOpen = false;

        // Search for file
        timer.Restart();
        var (model, version) = 
            await liteDbContext.FindCivitModelFromFileHashAsync(fileHash);
        
        timer.Stop();
        var timeTakenSearch = timer.Elapsed.TotalMilliseconds;

        var generalText =
            $"Time taken to hash: {timeTakenHash:F2} s\n" +
            $"Time taken to search: {timeTakenSearch:F1} ms\n";
        
        // Not found
        if (model == null)
        {
            var dialog = contentDialogService.CreateDialog();
            dialog.Title = "Model not found :(";
            dialog.Content = $"File not found in database. Hash: {fileHash}\n" + generalText;
            await dialog.ShowAsync();
        }
        else
        {
            // Found
            var dialog = contentDialogService.CreateDialog();
            dialog.Title = "Model found!";
            dialog.Content = $"File found in database. Hash: {fileHash}\n" +
                             $"Model: {model.Name}\n" +
                             $"Version: {version!.Name}\n" + generalText; 
            await dialog.ShowAsync();
        }
    }

    [RelayCommand]
    private async Task WebViewDemo()
    {
        var enterUri = await dialogFactory.ShowTextEntryDialog("Enter a URI", 
            new[] {
                ("Enter URI", "https://lykos.ai")
        });
        if (enterUri == null) return;
        
        var uri = new Uri(enterUri.First());
        var dialog = dialogFactory.CreateWebLoginDialog();
        var loginViewModel = dialog.ViewModel;
        loginViewModel.CurrentUri = uri;

        var dialogResult = await dialog.ShowAsync();
        logger.LogInformation("LoginDialog result: {Result}", dialogResult);
    }

    [RelayCommand]
    private async Task PingWebApi()
    {
        var result = await snackbarService.TryAsync(a3WebApiManager.Client.GetPing(), "Failed to ping web api");

        if (result.IsSuccessful)
        {
            var dialog = contentDialogService.CreateDialog();
            dialog.Title = "Web API ping";
            dialog.Content = result;
            dialog.PrimaryButtonText = "Ok";
            await dialog.ShowAsync();
        }
    }

    private async Task<bool> TryPingWebApi()
    {
        await using var minimumDelay = new MinimumDelay(100, 200);
        try
        {
            await a3WebApiManager.Client.GetPing();
            return true;
        }
        catch (TimeoutRejectedException)
        {
            logger.LogInformation("Ping timed out");
            return false;
        }
        catch (ApiException ex)
        {
            logger.LogInformation("Ping failed with status [{StatusCode}]: {Content}", ex.StatusCode, ex.ReasonPhrase);
            return false;
        }
    }
    
    [RelayCommand]
    private void OpenAppDataDirectory()
    {
        // Open app data in file explorer
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appPath = Path.Combine(appDataPath, "StabilityMatrix");
        Process.Start("explorer.exe", appPath);
    }
    
    [RelayCommand]
    private void OpenLibraryDirectory()
    {
        Process.Start("explorer.exe", settingsManager.LibraryDir);
    }

    [RelayCommand]
    private async Task OpenLicenseDialog()
    {
        IEnumerable<LicenseInfo> licenses;
        // Read json
        try
        {
            var stream = Application.GetResourceStream(new Uri(LicensesPath));
            using var reader = new StreamReader(stream!.Stream);
            var licenseText = await reader.ReadToEndAsync();
            licenses = JsonSerializer.Deserialize<IEnumerable<LicenseInfo>>(licenseText)
                ?? throw new Exception("Failed to deserialize licenses");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read licenses");
            snackbarService.ShowSnackbarAsync(
                "An embedded resource could not be read. Please try reinstalling the application.", 
                "Failed to read 'licenses.json'").SafeFireAndForget();
            return;
        }

        var flowViewer = new FlowDocumentScrollViewer
        {
            MaxHeight = 400,
            MaxWidth = 600,
        };
        var markdownText = "";
        foreach (var license in licenses)
        {
            markdownText += $"## [**{license.PackageName}**]({license.PackageUrl}) by {string.Join(", ", license.Authors)}\n\n";
            markdownText += $"{license.Copyright}\n\n";
            markdownText += $"[{license.LicenseUrl}]({license.LicenseUrl})\n\n";
        }
        flowViewer.Document = TextToFlowDocumentConverter!.Convert(markdownText, typeof(FlowDocument), null!, CultureInfo.CurrentCulture) as FlowDocument;

        var dialog = contentDialogService.CreateDialog();
        dialog.Title = "License and Open Source Notices";
        dialog.Content = flowViewer;
        dialog.DialogMaxHeight = 1000;
        dialog.DialogMaxWidth = 900;
        dialog.IsPrimaryButtonEnabled = false;
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Click app version 7 times to enable debug options
    /// </summary>
    [RelayCommand]
    private async Task AppVersionClickAsync()
    {
        // Ignore if already enabled
        if (IsDebugModeEnabled) return;
        switch (AppVersionClickCount)
        {
            // Open flyout on 3rd click
            case 2:
                // Show the flyout
                IsVersionFlyoutOpen = true;
                AppVersionClickCount++;
                break;
            // Reached threshold
            case AppVersionClickCountThreshold - 1:
            {
                // Close flyout
                IsVersionFlyoutOpen = false;
                // Enable debug options
                IsDebugModeEnabled = true;
                const string msg = "Warning: Improper use may corrupt application state or cause loss of data.";
                var dialog = snackbarService.ShowSnackbarAsync(msg, "Debug options enabled",
                    ControlAppearance.Info);
                await dialog;
                break;
            }
            default:
                // Otherwise, increment click count
                AppVersionClickCount++;
                break;
        }
    }

    [RelayCommand]
    [DoesNotReturn]
    private void DebugTriggerException()
    {
        throw new Exception("Test exception");
    }

    private void ApplyTheme(string value)
    {
        switch (value)
        {
            case "Light":
                Theme.Apply(ThemeType.Light);
                break;
            case "Dark":
                Theme.Apply(ThemeType.Dark);
                break;
            case "System":
                Theme.Apply(SystemInfo.ShouldUseDarkMode() ? ThemeType.Dark : ThemeType.Light);
                break;
        }
    }

    // Sets default port and host for web api fields
    public void SetWebApiDefaults()
    {
        // Set from launch options
        WebApiActivePackageHost = settingsManager.GetActivePackageHost();
        WebApiActivePackagePort = settingsManager.GetActivePackagePort();
        // Okay if both not empty
        if (!string.IsNullOrWhiteSpace(WebApiActivePackageHost) && 
            !string.IsNullOrWhiteSpace(WebApiActivePackagePort)) return;
        
        // Also check default values
        var currentInstall = settingsManager.Settings.GetActiveInstalledPackage();
        if (currentInstall?.PackageName == null) return;
        var currentPackage = packageFactory.FindPackageByName(currentInstall.PackageName);
        if (currentPackage == null) return;
        // Set default port and host
        WebApiActivePackageHost ??= currentPackage.LaunchOptions
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == "host")?.DefaultValue as string;
        WebApiActivePackagePort ??= currentPackage.LaunchOptions
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == "port")?.DefaultValue as string;
    }

    public void OnLoaded()
    {
        SelectedTheme = string.IsNullOrWhiteSpace(settingsManager.Settings.Theme)
            ? "Dark"
            : settingsManager.Settings.Theme;

        TestProperty = $"{SystemParameters.PrimaryScreenHeight} x {SystemParameters.PrimaryScreenWidth}";
        
        // Set defaults
        SetWebApiDefaults();
        // Refresh text2image connection badge
        Text2ImageRefreshBadge.RefreshFunc = TryPingWebApi;
        Text2ImageRefreshBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
}
