using System;
using System.Collections.ObjectModel;
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

namespace StabilityMatrix.Avalonia.ViewModels;

[View(typeof(SettingsPage))]
public partial class SettingsViewModel : PageViewModelBase
{
    private readonly INotificationService notificationService;
    
    public override string Title => "Settings";
    public override Symbol Icon => Symbol.Setting;
    
    public SettingsViewModel(INotificationService notificationService)
    {
        this.notificationService = notificationService;

        SelectedTheme = AvailableThemes[1];
    }
    
    // Theme panel
    [ObservableProperty]
    private string? selectedTheme;
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Light",
        "Dark",
        "System",
    };

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
    [ObservableProperty]
    private string? debugPaths;

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
    
    
    
    public override bool CanNavigateNext { get; protected set; }
    public override bool CanNavigatePrevious { get; protected set; }
}
