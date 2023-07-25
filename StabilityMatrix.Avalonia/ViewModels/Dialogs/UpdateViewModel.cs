using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;
using StabilityMatrix.Core.Updater;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(UpdateDialog))]
public partial class UpdateViewModel : ContentDialogViewModelBase
{
    private readonly ISettingsManager settingsManager;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IUpdateHelper updateHelper;

    [ObservableProperty] private bool isUpdateAvailable;
    [ObservableProperty] private UpdateInfo? updateInfo;
    
    [ObservableProperty] private string? releaseNotes;
    [ObservableProperty] private string? updateText;
    [ObservableProperty] private int progressValue;
    [ObservableProperty] private bool showProgressBar;
    
    public UpdateViewModel(
        ISettingsManager settingsManager,
        IHttpClientFactory httpClientFactory, 
        IUpdateHelper updateHelper)
    {
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
        this.updateHelper = updateHelper;

        EventManager.Instance.UpdateAvailable += (_, info) =>
        {
            IsUpdateAvailable = true;
            UpdateInfo = info;
        };
        updateHelper.StartCheckingForUpdates().SafeFireAndForget();
    }
    
    public override async Task OnLoadedAsync()
    {
        UpdateText = $"Stability Matrix v{UpdateInfo?.Version} is now available! You currently have v{Utilities.GetAppVersion()}. Would you like to update now?";
        
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(UpdateInfo?.ChangelogUrl);
        if (response.IsSuccessStatusCode)
        {
            ReleaseNotes = await response.Content.ReadAsStringAsync();
        }
        else
        {
            ReleaseNotes = "## Unable to load release notes";
        }
    }

    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (UpdateInfo == null)
        {
            return;
        }
        
        ShowProgressBar = true;
        UpdateText = $"Downloading update v{UpdateInfo.Version}...";
        await updateHelper.DownloadUpdate(UpdateInfo, new Progress<ProgressReport>(report =>
        {
            ProgressValue = Convert.ToInt32(report.Percentage);
        }));
        
        UpdateText = "Update complete. Restarting Stability Matrix in 3 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 2 seconds...";
        await Task.Delay(1000);
        UpdateText = "Update complete. Restarting Stability Matrix in 1 second...";
        await Task.Delay(1000);

        Process.Start(UpdateHelper.ExecutablePath);
        App.Shutdown();
    }
}
