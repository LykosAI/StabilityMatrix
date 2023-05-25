using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
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
    private string selectedTheme;
    private readonly IContentDialogService contentDialogService;
    
    public SettingsViewModel(ISettingsManager settingsManager, IContentDialogService contentDialogService)
    {
        this.settingsManager = settingsManager;
        this.contentDialogService = contentDialogService;
        SelectedTheme = settingsManager.Settings.Theme;
    }

    public string SelectedTheme
    {
        get => selectedTheme;
        set
        {
            if (value == selectedTheme) return;
            selectedTheme = value;
            OnPropertyChanged();
            
            switch (selectedTheme)
            {
                case "Light":
                    Theme.Apply(ThemeType.Light);
                    break;
                case "Dark":
                    Theme.Apply(ThemeType.Dark);
                    break;
            }

            settingsManager.SetTheme(selectedTheme);
        }
    }
    
    [ObservableProperty]
    public string gpuInfo =
        $"{HardwareHelper.GetGpuChipName()} ({HardwareHelper.GetGpuMemoryBytes() / 1024 / 1024 / 1024} GB)";

    [ObservableProperty]
    public string gitInfo;

    [ObservableProperty] 
    public string testProperty;
    
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

    public async Task OnLoaded()
    {
        SelectedTheme = string.IsNullOrWhiteSpace(settingsManager.Settings.Theme)
            ? "Dark"
            : settingsManager.Settings.Theme;
        GitInfo = await ProcessRunner.GetProcessOutputAsync("git", "--version");
        
        TestProperty = $"{SystemParameters.PrimaryScreenHeight} x {SystemParameters.PrimaryScreenWidth}";
    }
}
