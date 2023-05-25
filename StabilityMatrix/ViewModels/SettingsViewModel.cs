using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Helper;
using Wpf.Ui.Appearance;

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
    
    public SettingsViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
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

    public async Task OnLoaded()
    {
        SelectedTheme = string.IsNullOrWhiteSpace(settingsManager.Settings.Theme)
            ? "Dark"
            : settingsManager.Settings.Theme;
        GitInfo = await ProcessRunner.GetProcessOutputAsync("git", "--version");
    }
}
