using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Models.Update;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Updater;

[Singleton(typeof(IUpdateHelper))]
public class UpdateHelper : IUpdateHelper
{
    private readonly ILogger<UpdateHelper> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IDownloadService downloadService;
    private readonly ISettingsManager settingsManager;
    private readonly DebugOptions debugOptions;
    private readonly System.Timers.Timer timer = new(TimeSpan.FromMinutes(60));

    private string UpdateManifestUrl =>
        debugOptions.UpdateManifestUrl ?? "https://cdn.lykos.ai/update-v3.json";

    public const string UpdateFolderName = ".StabilityMatrixUpdate";
    public static DirectoryPath UpdateFolder => Compat.AppCurrentDir.JoinDir(UpdateFolderName);

    public static FilePath ExecutablePath => UpdateFolder.JoinFile(Compat.GetExecutableName());

    public UpdateHelper(
        ILogger<UpdateHelper> logger,
        IHttpClientFactory httpClientFactory,
        IDownloadService downloadService,
        IOptions<DebugOptions> debugOptions,
        ISettingsManager settingsManager
    )
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.downloadService = downloadService;
        this.settingsManager = settingsManager;
        this.debugOptions = debugOptions.Value;

        timer.Elapsed += async (_, _) =>
        {
            await CheckForUpdate().ConfigureAwait(false);
        };
    }

    public async Task StartCheckingForUpdates()
    {
        timer.Enabled = true;
        timer.Start();
        await CheckForUpdate().ConfigureAwait(false);
    }

    public async Task DownloadUpdate(UpdateInfo updateInfo, IProgress<ProgressReport> progress)
    {
        UpdateFolder.Create();
        UpdateFolder.Info.Attributes |= FileAttributes.Hidden;

        // download the file from URL
        await downloadService
            .DownloadToFileAsync(
                updateInfo.Url.ToString(),
                ExecutablePath,
                progress: progress,
                httpClientName: "UpdateClient"
            )
            .ConfigureAwait(false);
    }

    private async Task CheckForUpdate()
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("UpdateClient");
            var response = await httpClient.GetAsync(UpdateManifestUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Error while checking for update {StatusCode} - {Content}",
                    response.StatusCode,
                    await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                );
                return;
            }

            var updateManifest = await JsonSerializer
                .DeserializeAsync<UpdateManifest>(
                    await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                )
                .ConfigureAwait(false);

            if (updateManifest is null)
            {
                logger.LogError("UpdateManifest is null");
                return;
            }

            foreach (
                var channel in Enum.GetValues(typeof(UpdateChannel))
                    .Cast<UpdateChannel>()
                    .Where(c => c > UpdateChannel.Unknown)
            )
            {
                if (
                    updateManifest.Updates.TryGetValue(channel, out var platforms)
                    && platforms.GetInfoForCurrentPlatform() is { } update
                    && ValidateUpdate(update)
                )
                {
                    NotifyUpdateAvailable(update);
                    return;
                }
            }

            logger.LogInformation("No update available");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Couldn't check for update");
        }
    }

    private bool ValidateUpdate(UpdateInfo? update)
    {
        if (update is null)
            return false;

        // Verify signature
        var checker = new SignatureChecker();
        var signedData = update.GetSignedData();

        if (!checker.Verify(signedData, update.Signature))
        {
            logger.LogError(
                "UpdateInfo signature {Signature} is invalid, Data = {Data}, UpdateInfo = {Info}",
                update.Signature,
                signedData,
                update
            );
            return false;
        }

        switch (update.Version.ComparePrecedenceTo(Compat.AppVersion))
        {
            case > 0:
                // Newer version available
                return true;
            case 0:
            {
                // Same version available, check if we both have commit hash metadata
                var updateHash = update.Version.Metadata;
                var appHash = Compat.AppVersion.Metadata;
                // If different, we can update
                if (updateHash != appHash)
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }

    private void NotifyUpdateAvailable(UpdateInfo update)
    {
        logger.LogInformation(
            "Update available {AppVer} -> {UpdateVer}",
            Compat.AppVersion,
            update.Version
        );
        EventManager.Instance.OnUpdateAvailable(update);
    }
}
