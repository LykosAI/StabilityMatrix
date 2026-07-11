using System.Text.Json;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Semver;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Configs;
using StabilityMatrix.Core.Models.Notifications;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<IAppNotificationService, AppNotificationService>]
public class AppNotificationService(
    ILogger<AppNotificationService> logger,
    IHttpClientFactory httpClientFactory,
    ISettingsManager settingsManager,
    IOptions<DebugOptions> debugOptions
) : IAppNotificationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DebugOptions debugOptions = debugOptions.Value;

    private string NotificationsUrl =>
        debugOptions.NotificationsUrl ?? "https://cdn.lykos.ai/notifications/notifications.json";

    // In-memory cache
    private AppNotificationManifest? cachedManifest;
    private DateTimeOffset lastFetchTime = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public AppNotification? CurrentNotification { get; private set; }

    /// <inheritdoc />
    public async Task<AppNotification?> CheckForNotificationsAsync()
    {
        var manifest = await FetchManifestAsync().ConfigureAwait(false);

        if (manifest is null)
        {
            CurrentNotification = null;
            return null;
        }

        CurrentNotification = GetActiveNotification(manifest);
        return CurrentNotification;
    }

    /// <inheritdoc />
    public void Dismiss(string notificationId)
    {
        settingsManager.Transaction(s =>
        {
            if (!s.DismissedNotificationIds.Contains(notificationId))
            {
                s.DismissedNotificationIds.Add(notificationId);
            }
        });

        // Clear current if it matches
        if (CurrentNotification?.Id == notificationId)
        {
            CurrentNotification = null;
        }
    }

    /// <inheritdoc />
    public string? ResolveLocalizedString(Dictionary<string, string>? localizedStrings)
    {
        if (localizedStrings is null or { Count: 0 })
            return null;

        var language = settingsManager.Settings.Language ?? "en-US";

        // Try exact match (e.g. "en-US")
        if (localizedStrings.TryGetValue(language, out var exact))
            return exact;

        // Try language prefix (e.g. "en-US" -> "en")
        var prefix = language.Split('-')[0];
        if (localizedStrings.TryGetValue(prefix, out var prefixed))
            return prefixed;

        // Fallback to "en"
        if (localizedStrings.TryGetValue("en", out var english))
            return english;

        return null;
    }

    private async Task<AppNotificationManifest?> FetchManifestAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Return cached if still fresh
        if (cachedManifest is not null && now - lastFetchTime < CacheTtl)
        {
            return cachedManifest;
        }

        try
        {
            AppNotificationManifest? manifest;

            if (NotificationsUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var filePath = NotificationsUrl[7..].Replace('/', Path.DirectorySeparatorChar);
                if (Compat.IsWindows && filePath.StartsWith('\\'))
                    filePath = filePath[1..];

                await using var stream = File.OpenRead(filePath);
                manifest = await JsonSerializer
                    .DeserializeAsync<AppNotificationManifest>(stream, JsonOptions)
                    .ConfigureAwait(false);
            }
            else
            {
                using var httpClient = httpClientFactory.CreateClient("AppNotificationClient");
                using var response = await httpClient.GetAsync(NotificationsUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Failed to fetch notifications: {StatusCode}", response.StatusCode);
                    return GetCachedManifestIfValid();
                }

                manifest = await JsonSerializer
                    .DeserializeAsync<AppNotificationManifest>(
                        await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                        JsonOptions
                    )
                    .ConfigureAwait(false);
            }

            if (manifest is null)
            {
                logger.LogWarning("Deserialized notification manifest is null");
                return GetCachedManifestIfValid();
            }

            // Update cache
            cachedManifest = manifest;
            lastFetchTime = now;

            logger.LogDebug(
                "Fetched notification manifest with {Count} notifications",
                manifest.Notifications.Count
            );

            return manifest;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Error fetching notification manifest");
            return GetCachedManifestIfValid();
        }
    }

    /// <summary>
    /// Return the cached manifest only if at least one notification is still within its time window.
    /// This handles the "kill switch" case: if all notifications have expired, we return null
    /// even when offline, so the banner disappears.
    /// </summary>
    private AppNotificationManifest? GetCachedManifestIfValid()
    {
        if (cachedManifest is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        var hasValidNotification = cachedManifest.Notifications.Any(n =>
            n.EndDate is null || n.EndDate > now
        );

        return hasValidNotification ? cachedManifest : null;
    }

    /// <summary>
    /// Filter and sort notifications to find the single active one.
    /// </summary>
    private AppNotification? GetActiveNotification(AppNotificationManifest manifest)
    {
        var now = DateTimeOffset.UtcNow;
        var dismissedIds = settingsManager.Settings.DismissedNotificationIds;

        return manifest
            .Notifications.Where(n =>
            {
                // Time window check
                if (n.StartDate is not null && now < n.StartDate)
                    return false;
                if (n.EndDate is not null && now > n.EndDate)
                    return false;

                // Dismissal check
                if (dismissedIds.Contains(n.Id))
                    return false;

                // Version check (optional)
                if (n.MinVersion is not null && SemVersion.TryParse(n.MinVersion, out var minVer))
                {
                    if (Compat.AppVersion.ComparePrecedenceTo(minVer) < 0)
                        return false;
                }

                if (n.MaxVersion is not null && SemVersion.TryParse(n.MaxVersion, out var maxVer))
                {
                    if (Compat.AppVersion.ComparePrecedenceTo(maxVer) >= 0)
                        return false;
                }

                // First-launch setup check, delay notification until a subsequent session
                if (n.RequireSetupComplete && !(settingsManager.Settings.FirstLaunchSetupComplete))
                    return false;

                // Locale check — must have a resolvable message
                if (ResolveLocalizedString(n.Message) is null)
                    return false;

                return true;
            })
            .OrderByDescending(n => n.Priority)
            .FirstOrDefault();
    }
}
