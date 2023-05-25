using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using Wpf.Ui.Appearance;

namespace StabilityMatrix.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;

    public MainWindowViewModel(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;
    }

    [ObservableProperty] 
    private Visibility advancedModeVisibility;

    [ObservableProperty]
    private Visibility simpleModeVisibility;

    public void OnLoaded()
    {
        SetTheme();
        GoAdvancedMode();
    }
    
    [RelayCommand]
    private void GoAdvancedMode()
    {
        AdvancedModeVisibility = Visibility.Visible;
        SimpleModeVisibility = Visibility.Hidden;
    }
    
    private void SetTheme()
    {
        var theme = settingsManager.Settings.Theme;
        switch (theme)
        {
            case "Dark":
                Theme.Apply(ThemeType.Dark);
                break;
            case "Light":
                Theme.Apply(ThemeType.Light);
                break;
        }
    }
}
