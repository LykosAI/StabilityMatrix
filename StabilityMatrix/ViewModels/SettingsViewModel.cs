using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
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
using StabilityMatrix.Api;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Python;
using Wpf.Ui.Appearance;
using Wpf.Ui.Contracts;
using Wpf.Ui.Controls.Window;
using EventManager = StabilityMatrix.Helper.EventManager;
using ISnackbarService = StabilityMatrix.Helper.ISnackbarService;

namespace StabilityMatrix.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILogger<SettingsViewModel> logger;
    private readonly ISettingsManager settingsManager;
    private readonly IPackageFactory packageFactory;
    private readonly IPyRunner pyRunner;
    private readonly ISnackbarService snackbarService;
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


    [ObservableProperty] private string? webApiHost;
    [ObservableProperty] private string? webApiPort;
    [ObservableProperty] private string? webApiActivePackageHost;
    [ObservableProperty] private string? webApiActivePackagePort;
    
    partial void OnWebApiHostChanged(string? value)
    {
        settingsManager.SetWebApiHost(value);
        a3WebApiManager.ResetClient();
    }
    
    partial void OnWebApiPortChanged(string? value)
    {
        settingsManager.SetWebApiPort(value);
        a3WebApiManager.ResetClient();
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
        IA3WebApiManager a3WebApiManager, 
        IPyRunner pyRunner, 
        ISnackbarService snackbarService, 
        ILogger<SettingsViewModel> logger, 
        IPackageFactory packageFactory)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.packageFactory = packageFactory;
        this.contentDialogService = contentDialogService;
        this.snackbarService = snackbarService;
        this.a3WebApiManager = a3WebApiManager;
        this.pyRunner = pyRunner;
        SelectedTheme = settingsManager.Settings.Theme ?? "Dark";
        WindowBackdropType = settingsManager.Settings.WindowBackdropType ?? WindowBackdropType.Mica;
    }

    [ObservableProperty]
    private bool isDebugModeEnabled;
    partial void OnIsDebugModeEnabledChanged(bool value) => EventManager.Instance.OnDevModeSettingChanged(value);
    
    [ObservableProperty]
    private string selectedTheme;
    
    [ObservableProperty] 
    private WindowBackdropType windowBackdropType;

    public string AppVersion => $"Version {GetAppVersion()}";
    
    private string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fvi = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
        return fvi ?? "(Unknown)";
    }
    
    partial void OnSelectedThemeChanged(string value)
    {
        settingsManager.SetTheme(value);
        ApplyTheme(value);
    }

    partial void OnWindowBackdropTypeChanged(WindowBackdropType oldValue, WindowBackdropType newValue)
    {
        settingsManager.SetWindowBackdropType(newValue);
        if (Application.Current.MainWindow != null)
        {
            WindowBackdrop.ApplyBackdrop(Application.Current.MainWindow, newValue);
        }
    }

    [ObservableProperty]
    private string gpuInfo =
        $"{HardwareHelper.GetGpuChipName()} ({HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024} GB)";

    [ObservableProperty] private string? gitInfo;

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
            Path = path,
            PackageName = "dank-diffusion",
            PackageVersion = "v1.0.0",
        };

        // Add package to settings
        settingsManager.AddInstalledPackage(package);
    });

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

        var flowViewer = new FlowDocumentScrollViewer();
        var markdownText = "";
        foreach (var license in licenses)
        {
            markdownText += $"## [{license.PackageName}]({license.PackageUrl}) by {string.Join(", ", license.Authors)}\n\n";
            markdownText += $"{license.Copyright}\n\n";
            markdownText += $"[{license.LicenseUrl}]({license.LicenseUrl})\n\n";
        }
        flowViewer.Document = TextToFlowDocumentConverter!.Convert(markdownText, typeof(FlowDocument), null, CultureInfo.CurrentCulture) as FlowDocument;

        var dialog = contentDialogService.CreateDialog();
        dialog.Title = "License and Open Source Notices";
        dialog.Content = flowViewer;
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
                    LogLevel.Information);
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
                Theme.Apply(ThemeType.Light, WindowBackdropType);
                break;
            case "Dark":
                Theme.Apply(ThemeType.Dark, WindowBackdropType);
                break;
            case "System":
                Theme.Apply(SystemInfo.ShouldUseDarkMode() ? ThemeType.Dark : ThemeType.Light, WindowBackdropType);
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
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == "host")?.DefaultValue as string; ;
        WebApiActivePackagePort ??= currentPackage.LaunchOptions
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == "port")?.DefaultValue as string;
    }

    public async Task OnLoaded()
    {
        SelectedTheme = string.IsNullOrWhiteSpace(settingsManager.Settings.Theme)
            ? "Dark"
            : settingsManager.Settings.Theme;
        GitInfo = await ProcessRunner.GetProcessOutputAsync("git", "--version");

        TestProperty = $"{SystemParameters.PrimaryScreenHeight} x {SystemParameters.PrimaryScreenWidth}";
        
        // Set defaults
        SetWebApiDefaults();
        // Refresh text2image connection badge
        Text2ImageRefreshBadge.RefreshFunc = TryPingWebApi;
        Text2ImageRefreshBadge.RefreshCommand.ExecuteAsync(null).SafeFireAndForget();
    }
}
