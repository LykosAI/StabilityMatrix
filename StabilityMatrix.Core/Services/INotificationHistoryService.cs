using StabilityMatrix.Core.Models.Notifications;

namespace StabilityMatrix.Core.Services;

/// <summary>
/// Session-only history of notifications shown by <see cref="StabilityMatrix.Avalonia.Services.INotificationService"/>.
/// Populated regardless of whether the user has the corresponding NotificationKey suppressed, so suppressed events
/// are still visible in the activity panel.
/// </summary>
public interface INotificationHistoryService
{
    IReadOnlyList<NotificationHistoryEntry> Entries { get; }

    /// <summary>Total number of entries. O(1) — avoids the snapshot allocation of <see cref="Entries"/>.</summary>
    int Count { get; }

    int UnreadCount { get; }

    event EventHandler<NotificationHistoryEntry>? EntryAdded;

    /// <summary>Raised on bulk changes (clear / mark-all-read / remove) where per-entry events would be noisy.</summary>
    event EventHandler? EntriesChanged;

    /// <summary>Add an entry. Returns the same entry (with a fresh Id if none was set) so callers can correlate.</summary>
    NotificationHistoryEntry Add(NotificationHistoryEntry entry);

    void MarkRead(Guid id);

    void MarkAllRead();

    void Remove(Guid id);

    void Clear();

    /// <summary>Look up an entry by Id, or null if it has been evicted.</summary>
    NotificationHistoryEntry? Find(Guid id);
}
