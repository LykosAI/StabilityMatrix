using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Models;
using StabilityMatrix.Services;

namespace StabilityMatrix.Helper;

public class UpdateHelper : IUpdateHelper
{
    private readonly ILogger<UpdateHelper> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IDownloadService downloadService;
    private readonly DispatcherTimer timer = new();
    
    private static readonly string UpdateFolder =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update");

    public static readonly string ExecutablePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update", "StabilityMatrix.exe");

    public UpdateHelper(ILogger<UpdateHelper> logger, IHttpClientFactory httpClientFactory,
        IDownloadService downloadService)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.downloadService = downloadService;

        timer.Interval = TimeSpan.FromMinutes(5);
        timer.Tick += async (_, _) => { await CheckForUpdate(); };
    }

    public async Task StartCheckingForUpdates()
    {
        timer.IsEnabled = true;
        timer.Start();
        await CheckForUpdate();
    }

    public async Task DownloadUpdate(UpdateInfo updateInfo,
        IProgress<ProgressReport> progress)
    {
        var downloadUrl = updateInfo.DownloadUrl;

        Directory.CreateDirectory(UpdateFolder);
        
        // download the file from URL
        await downloadService.DownloadToFileAsync(downloadUrl, ExecutablePath, progress: progress,
            httpClientName: "UpdateClient");
    }

    private async Task CheckForUpdate()
    {
        var httpClient = httpClientFactory.CreateClient("UpdateClient");
        var response = await httpClient.GetAsync("https://cdn.lykos.ai/update.json");
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error while checking for update");
            return;
        }

        var updateInfo =
            await JsonSerializer.DeserializeAsync<UpdateInfo>(
                await response.Content.ReadAsStreamAsync());

        if (updateInfo == null)
        {
            logger.LogError("UpdateInfo is null");
            return;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
         
        if (updateInfo.Version <= currentVersion)
        {
            logger.LogInformation("No update available");
            return;
        }

        logger.LogInformation("Update available");
        EventManager.Instance.OnUpdateAvailable(updateInfo);
    }
}
