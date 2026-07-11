using StabilityMatrix.Core.Models.Notifications;

namespace StabilityMatrix.Core.Services;

public interface IAppNotificationService
{
    /// <summary>
    /// The currently active notification (highest priority, non-dismissed, within time window).
    /// Null if no notification is active.
    /// </summary>
    AppNotification? CurrentNotification { get; }

    /// <summary>
    /// Fetch the notification manifest from CDN and resolve the active notification.
    /// Returns null if no notification is active or fetch fails with no valid cache.
    /// </summary>
    Task<AppNotification?> CheckForNotificationsAsync();

    /// <summary>
    /// Mark a notification as dismissed. Persists the ID to settings.
    /// </summary>
    void Dismiss(string notificationId);

    /// <summary>
    /// Resolve a localized string from a locale dictionary, using the user's
    /// configured language with fallback to "en".
    /// </summary>
    /// <returns>The resolved string, or null if no matching locale found.</returns>
    string? ResolveLocalizedString(Dictionary<string, string>? localizedStrings);
}
