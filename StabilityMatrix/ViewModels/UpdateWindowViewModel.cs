using System.Net.Http;
using System.Threading.Tasks;
using AutoUpdaterDotNET;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Helper;
using Wpf.Ui.Controls.Window;

namespace StabilityMatrix.ViewModels;

public partial class UpdateWindowViewModel : ObservableObject
{
    private readonly ISettingsManager settingsManager;
    private readonly IHttpClientFactory httpClientFactory;

    public UpdateWindowViewModel(ISettingsManager settingsManager, IHttpClientFactory httpClientFactory)
    {
        this.settingsManager = settingsManager;
        this.httpClientFactory = httpClientFactory;
    }

    [ObservableProperty] private string? releaseNotes;

    public UpdateInfoEventArgs? UpdateInfo { get; set; }
    public WindowBackdropType WindowBackdropType => settingsManager.Settings.WindowBackdropType ??
                                                    WindowBackdropType.Mica;

    public async Task OnLoaded()
    {
        using var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(UpdateInfo?.ChangelogURL);
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
    private void InstallUpdate()
    {
        if (AutoUpdater.DownloadUpdate(UpdateInfo))
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
