using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using StabilityMatrix.Helper;
using StabilityMatrix.Models.Configs;
using StabilityMatrix.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls.Window;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IDialogFactory dialogFactory;
    private readonly INotificationBarService notificationBarService;
    private readonly DebugOptions debugOptions;

    public MainWindowViewModel(ISettingsManager settingsManager, IDialogFactory dialogFactory, INotificationBarService notificationBarService, IOptions<DebugOptions> debugOptions)
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationBarService = notificationBarService;
        this.debugOptions = debugOptions.Value;
        
        // Listen to dev mode event
        EventManager.Instance.DevModeSettingChanged += (_, value) => IsTextToImagePageEnabled = value;
    }

    [ObservableProperty]
    private float progressValue;

    [ObservableProperty] 
    private bool isIndeterminate;

    [ObservableProperty]   
    private TaskbarItemProgressState progressState;

    [ObservableProperty]
    private bool isTextToImagePageEnabled;

    public async Task OnLoaded()
    {
        SetTheme();
        EventManager.Instance.GlobalProgressChanged += OnGlobalProgressChanged;
        
        if (debugOptions.ShowOneClickInstall || !settingsManager.Settings.InstalledPackages.Any())
        {
            var dialog = dialogFactory.CreateOneClickInstallDialog();
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.IsFooterVisible = false;

            EventManager.Instance.OneClickInstallFinished += (_, _) =>
            {
                dialog.Hide();
                EventManager.Instance.OnTeachingTooltipNeeded();
            };

            await dialog.ShowAsync();
        }

        notificationBarService.ShowStartupNotifications();
    }
    
    [RelayCommand]
    private void OpenLinkPatreon()
    {
        ProcessRunner.OpenUrl("https://www.patreon.com/StabilityMatrix");
    }
    
    [RelayCommand]
    private void OpenLinkDiscord()
    {
        ProcessRunner.OpenUrl("https://discord.gg/TUrgfECxHz");
    }

    /// <summary>
    ///   Updates the taskbar progress bar value and state.
    /// </summary>
    /// <param name="progress">Progress value from 0 to 100</param>
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

    private void SetTheme()
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (Application.Current.MainWindow != null)
            {
                WindowBackdrop.ApplyBackdrop(Application.Current.MainWindow,
                    settingsManager.Settings.WindowBackdropType ?? WindowBackdropType.Mica);
            }

            var theme = settingsManager.Settings.Theme;
            switch (theme)
            {
                case "Dark":
                    Theme.Apply(ThemeType.Dark, settingsManager.Settings.WindowBackdropType ?? WindowBackdropType.Mica);
                    break;
                case "Light":
                    Theme.Apply(ThemeType.Light, settingsManager.Settings.WindowBackdropType ?? WindowBackdropType.Mica);
                    break;
            }
        });
    }
}
