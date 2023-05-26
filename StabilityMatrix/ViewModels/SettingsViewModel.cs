using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Contracts;

namespace StabilityMatrix.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;

    public ObservableCollection<string> AvailableThemes => new()
    {
        "Light",
        "Dark",
        "System",
    };
    private readonly IContentDialogService contentDialogService;

    public SettingsViewModel(ISettingsManager settingsManager, IContentDialogService contentDialogService)
    {
        this.settingsManager = settingsManager;
        this.contentDialogService = contentDialogService;
        SelectedTheme = settingsManager.Settings.Theme ?? "Dark";
    }

    [ObservableProperty] 
    private string selectedTheme;
    
    partial void OnSelectedThemeChanged(string value)
    {
        settingsManager.SetTheme(value);
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

    [ObservableProperty]
    private string gpuInfo =
        $"{HardwareHelper.GetGpuChipName()} ({HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024} GB)";

    [ObservableProperty] private string? gitInfo;

    [ObservableProperty] private string? testProperty;

    public AsyncRelayCommand PythonVersionCommand => new(async () =>
    {
        // Get python version
        await PyRunner.Initialize();
        var result = await PyRunner.GetVersionInfo();
        // Show dialog box
        var dialog = contentDialogService.CreateDialog();
        dialog.Title = "Python version info";
        dialog.Content = result;
        dialog.PrimaryButtonText = "Ok";
        await dialog.ShowAsync();
    });

    public RelayCommand AddInstallationCommand => new(() =>
    {
        var initialDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\StabilityMatrix\\Packages";
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
            Name = Path.GetFileName(path),
            Path = path,
            PackageName = "dank-diffusion",
            PackageVersion = "v1.0.0",
        };

        // Add package to settings
        settingsManager.AddInstalledPackage(package);
    });

    public async Task OnLoaded()
    {
        SelectedTheme = string.IsNullOrWhiteSpace(settingsManager.Settings.Theme)
            ? "Dark"
            : settingsManager.Settings.Theme;
        GitInfo = await ProcessRunner.GetProcessOutputAsync("git", "--version");

        TestProperty = $"{SystemParameters.PrimaryScreenHeight} x {SystemParameters.PrimaryScreenWidth}";
    }
}
