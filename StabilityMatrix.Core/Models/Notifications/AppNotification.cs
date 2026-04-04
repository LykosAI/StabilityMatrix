using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Notifications;

/// <summary>
/// Root manifest for server-pushed notifications.
/// Fetched from CDN on app launch.
/// </summary>
public record AppNotificationManifest
{
    public int Version { get; init; } = 1;
    public List<AppNotification> Notifications { get; init; } = [];
}

/// <summary>
/// A single server-pushed notification entry.
/// </summary>
public record AppNotification
{
    /// <summary>
    /// Unique ID used to track dismissal in local settings.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display type: banner (persistent strip) or dialog (modal on launch).
    /// </summary>
    public AppNotificationType Type { get; init; }

    /// <summary>
    /// Priority for ordering when multiple notifications are active.
    /// </summary>
    public AppNotificationPriority Priority { get; init; }

    /// <summary>
    /// Start of the active time window (UTC). If null, starts immediately.
    /// </summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>
    /// End of the active time window (UTC). If null, runs indefinitely.
    /// </summary>
    public DateTimeOffset? EndDate { get; init; }

    /// <summary>
    /// Minimum app version to show this notification (semver string, optional).
    /// </summary>
    public string? MinVersion { get; init; }

    /// <summary>
    /// Maximum app version to show this notification (semver string, optional).
    /// </summary>
    public string? MaxVersion { get; init; }

    /// <summary>
    /// When true, the notification is only shown after first-launch setup has been completed
    /// in a previous session. This prevents showing notifications during or immediately after
    /// the first-launch setup flow.
    /// </summary>
    public bool RequireSetupComplete { get; init; }

    /// <summary>
    /// Visual style for the banner.
    /// </summary>
    public AppNotificationStyle Style { get; init; } = new();

    /// <summary>
    /// Whether the banner can be dismissed via X button.
    /// </summary>
    public bool Dismissible { get; init; } = true;

    /// <summary>
    /// Action triggered by the banner's call-to-action button.
    /// </summary>
    public AppNotificationAction? Action { get; init; }

    /// <summary>
    /// Localized banner message text. Keys are locale codes (e.g. "en", "ja").
    /// </summary>
    public Dictionary<string, string> Message { get; init; } = new();

    /// <summary>
    /// Dialog content, used when <see cref="Action"/> type is <see cref="AppNotificationActionType.Dialog"/>.
    /// </summary>
    public AppNotificationDialog? Dialog { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<AppNotificationType>))]
public enum AppNotificationType
{
    Banner,
    Dialog,
}

[JsonConverter(typeof(JsonStringEnumConverter<AppNotificationPriority>))]
public enum AppNotificationPriority
{
    Low,
    Normal,
    High,
    Critical,
}

public record AppNotificationStyle
{
    /// <summary>
    /// Color variant: "info", "warning", "success", or "accent".
    /// </summary>
    public string Variant { get; init; } = "info";
}

public record AppNotificationAction
{
    /// <summary>
    /// Action type: open a URL or show an in-app dialog.
    /// </summary>
    public AppNotificationActionType Type { get; init; }

    /// <summary>
    /// URL to open when type is <see cref="AppNotificationActionType.Url"/>.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Localized button label text.
    /// </summary>
    public Dictionary<string, string> Label { get; init; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter<AppNotificationActionType>))]
public enum AppNotificationActionType
{
    Url,
    Dialog,
}

public record AppNotificationDialog
{
    /// <summary>
    /// Localized dialog title.
    /// </summary>
    public Dictionary<string, string> Title { get; init; } = new();

    /// <summary>
    /// Localized markdown content for the dialog body.
    /// </summary>
    public Dictionary<string, string> Content { get; init; } = new();

    /// <summary>
    /// Optional buttons at the bottom of the dialog.
    /// If omitted, a default dismiss button is shown.
    /// </summary>
    public List<AppNotificationButton>? Buttons { get; init; }
}

public record AppNotificationButton
{
    /// <summary>
    /// Button type: opens a URL or dismisses the dialog/notification.
    /// </summary>
    public AppNotificationButtonType Type { get; init; }

    /// <summary>
    /// URL to open when type is <see cref="AppNotificationButtonType.Url"/>.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Localized button label text.
    /// </summary>
    public Dictionary<string, string> Label { get; init; } = new();

    /// <summary>
    /// Visual style: "primary", "secondary", or "accent".
    /// </summary>
    public AppNotificationButtonStyle Style { get; init; } = AppNotificationButtonStyle.Secondary;

    /// <summary>
    /// Whether clicking this button also dismisses the notification.
    /// </summary>
    public bool DismissOnClick { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<AppNotificationButtonType>))]
public enum AppNotificationButtonType
{
    Url,
    Dismiss,
}

[JsonConverter(typeof(JsonStringEnumConverter<AppNotificationButtonStyle>))]
public enum AppNotificationButtonStyle
{
    /// <summary>
    /// Maps to the ContentDialog Primary button (accent-colored).
    /// </summary>
    Primary,

    /// <summary>
    /// Maps to the ContentDialog Secondary button.
    /// </summary>
    Secondary,

    /// <summary>
    /// Maps to the ContentDialog Close button (subtle / cancel styling).
    /// </summary>
    Close,

    /// <summary>
    /// Maps to the ContentDialog Primary button with accent style override.
    /// </summary>
    Accent,
}
