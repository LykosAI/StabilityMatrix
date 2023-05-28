using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Python.Runtime;
using StabilityMatrix.Helper;
using Wpf.Ui.Appearance;
using Dispatcher = System.Windows.Threading.Dispatcher;
using EventManager = StabilityMatrix.Helper.EventManager;

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
    
    [ObservableProperty]
    private float progressValue;

    [ObservableProperty] 
    private TaskbarItemProgressState progressState;

    public void OnLoaded()
    {
        SetTheme();
        GoAdvancedMode();
        EventManager.Instance.GlobalProgressChanged += OnGlobalProgressChanged;
    }

    private void OnGlobalProgressChanged(object? sender, int progress)
    {
        if (progress == -1)
        {
            ProgressState = TaskbarItemProgressState.Indeterminate;
            ProgressValue = 0;
        }
        else
        {
            ProgressState = TaskbarItemProgressState.Normal;
            ProgressValue = progress / 100f;
        }

        if (Math.Abs(ProgressValue - 1) < 0.01f)
        {
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    ProgressState = TaskbarItemProgressState.None;
                    ProgressValue = 0;
                });
            });
        }
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
