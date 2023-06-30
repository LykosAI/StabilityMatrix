using System;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        if (updateInfo.Version == Utilities.GetAppVersion())
        {
            logger.LogInformation("No update available");
            return;
        }

        // check if update is newer
        var updateVersion = updateInfo.Version.Split('.');
        var currentVersion = Utilities.GetAppVersion().Split('.');
        if (updateVersion.Length != 4 || currentVersion.Length != 4)
        {
            logger.LogError("Invalid version format");
            return;
        }

        var updateVersionInt = new int[4];
        var currentVersionInt = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (int.TryParse(updateVersion[i], out updateVersionInt[i]) &&
                int.TryParse(currentVersion[i], out currentVersionInt[i])) continue;
            logger.LogError("Invalid version format");
            return;
        }

        // check if update is newer
        var currentMajor = currentVersionInt[0];
        var currentMinor = currentVersionInt[1];
        var currentBuild = currentVersionInt[2];
        var currentRevision = currentVersionInt[3];
        
        var updateMajor = updateVersionInt[0];
        var updateMinor = updateVersionInt[1];
        var updateBuild = updateVersionInt[2];
        var updateRevision = updateVersionInt[3];
        
        if (updateMajor < currentMajor)
        {
            logger.LogInformation("No update available");
            return;
        }
        
        if (updateMajor == currentMajor && updateMinor < currentMinor)
        {
            logger.LogInformation("No update available");
            return;
        }
        
        if (updateMajor == currentMajor && updateMinor == currentMinor && updateBuild < currentBuild)
        {
            logger.LogInformation("No update available");
            return;
        }
        
        if (updateMajor == currentMajor && updateMinor == currentMinor && updateBuild == currentBuild && updateRevision <= currentRevision)
        {
            logger.LogInformation("No update available");
            return;
        }
        
        logger.LogInformation("Update available");
        EventManager.Instance.OnUpdateAvailable(updateInfo);
    }
}
