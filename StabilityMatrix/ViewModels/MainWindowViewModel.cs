using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Shell;
using AutoUpdaterDotNET;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using StabilityMatrix.Helper;
using StabilityMatrix.Models;
using StabilityMatrix.Models.Configs;
using StabilityMatrix.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls.ContentDialogControl;
using Wpf.Ui.Controls.Window;
using EventManager = StabilityMatrix.Helper.EventManager;

namespace StabilityMatrix.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IDialogFactory dialogFactory;
    private readonly INotificationBarService notificationBarService;
    private readonly UpdateWindowViewModel updateWindowViewModel;
    private readonly DebugOptions debugOptions;

    private UpdateInfo? updateInfo;

    public MainWindowViewModel(
        ISettingsManager settingsManager, 
        IDialogFactory dialogFactory, 
        INotificationBarService notificationBarService,
        UpdateWindowViewModel updateWindowViewModel,
        IOptions<DebugOptions> debugOptions)
    {
        this.settingsManager = settingsManager;
        this.dialogFactory = dialogFactory;
        this.notificationBarService = notificationBarService;
        this.updateWindowViewModel = updateWindowViewModel;
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

    [ObservableProperty] 
    private bool isUpdateAvailable;

    public async Task OnLoaded()
    {
        SetTheme();
        EventManager.Instance.GlobalProgressChanged += OnGlobalProgressChanged;
        EventManager.Instance.UpdateAvailable += (_, args) =>
        {
            IsUpdateAvailable = true;
            updateInfo = args;
        };
        
        // show path selection window if no paths are set
        await DoSettingsCheck();
        
        // Insert path extensions
        settingsManager.InsertPathExtensions();

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

    [RelayCommand]
    private void DoUpdate()
    {
        updateWindowViewModel.UpdateInfo = updateInfo;
        var updateWindow = new UpdateWindow(updateWindowViewModel);
        updateWindow.ShowDialog();
    }
    
    private async Task DoSettingsCheck()
    {
        // Check if library path is set
        if (!settingsManager.TryFindLibrary())
        {
            // See if this is an existing user for message variation
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsPath = Path.Combine(appDataPath, "StabilityMatrix", "settings.json");
            var isExistingUser = File.Exists(settingsPath);
            
            // need to show dialog to choose library location
            if (!settingsManager.TryFindLibrary())
            {
                var dialog = dialogFactory.CreateInstallLocationsDialog();
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    Application.Current.Shutdown();
                }

                // 1. For portable mode, call settings.SetPortableMode()
                var viewModel = (dialog.DataContext as SelectInstallLocationsViewModel)!;
                if (viewModel.IsPortableMode)
                {
                    settingsManager.SetPortableMode();
                }
                // 2. For custom path, call settings.SetLibraryPath(path)
                else
                {
                    settingsManager.SetLibraryPath(viewModel.DataDirectory);
                }
            }
        }
        
        // Try to find library again, should be found now
        if (!settingsManager.TryFindLibrary())
        {
            throw new Exception("Could not find library after setting path");
        }
        
        // Check if there are old packages, if so show migration dialog
        if (settingsManager.GetOldInstalledPackages().Any())
        {
            var dialog = dialogFactory.CreateDataDirectoryMigrationDialog();
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                Application.Current.Shutdown();
            }
        }
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
