using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.Views;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Services;
using Symbol = FluentIcons.Common.Symbol;
using SymbolIconSource = FluentIcons.FluentAvalonia.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
public partial class SettingsViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;
    private readonly ISettingsManager settingsManager;
    
    public override string Title => "Settings";
    public override IconSource IconSource => new SymbolIconSource {Symbol = Symbol.Settings, IsFilled = true};
    
    // Theme panel
    [ObservableProperty] private string? selectedTheme;
    
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light",
        "Dark",
        "System",
    };
    
    public SettingsViewModel(INotificationService notificationService, 
        ISettingsManager settingsManager)
    {
        this.notificationService = notificationService;
        this.settingsManager = settingsManager;

        SelectedTheme = AvailableThemes[1];
    }
    
    partial void OnSelectedThemeChanged(string? value)
    {
        // In case design / tests
        if (Application.Current is null) return;
        // Change theme
        Application.Current!.RequestedThemeVariant = value switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
    
    // Debug info
    [ObservableProperty] private string? debugPaths;
    [ObservableProperty] private string? debugCompatInfo;
    [ObservableProperty] private string? debugGpuInfo;

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
            Title = "Test title",
            PrimaryButtonText = "OK",
            CloseButtonText = "Close"
        };

        var result = await dialog.ShowAsync();
        notificationService.Show(new Notification("Content dialog closed",
            $"Result: {result}"));
    }
}
