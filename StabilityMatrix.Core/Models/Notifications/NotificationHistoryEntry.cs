using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Core.Models.Notifications;

/// <summary>
/// A persisted record of a single notification shown (or suppressed) during this session.
/// Exposed through <see cref="StabilityMatrix.Core.Services.INotificationHistoryService"/>.
/// </summary>
public sealed record NotificationHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public NotificationKey? Key { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Body { get; init; }

    public string? BodyImagePath { get; init; }

    public NotificationLevel Level { get; init; } = NotificationLevel.Information;

    public NotificationAction? Action { get; init; }

    /// <summary>Mutable so callers can flip read-state without having to replace the entry.</summary>
    public bool IsRead { get; set; }
}
