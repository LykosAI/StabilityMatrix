using Injectio.Attributes;
using StabilityMatrix.Core.Models.Notifications;

namespace StabilityMatrix.Core.Services;

[RegisterSingleton<INotificationHistoryService, NotificationHistoryService>]
public class NotificationHistoryService : INotificationHistoryService
{
    private const int MaxEntries = 100;

    private readonly LinkedList<NotificationHistoryEntry> entries = new();
    private readonly object sync = new();

    public IReadOnlyList<NotificationHistoryEntry> Entries
    {
        get
        {
            lock (sync)
                return entries.ToList();
        }
    }

    public int UnreadCount
    {
        get
        {
            lock (sync)
                return entries.Count(e => !e.IsRead);
        }
    }

    public event EventHandler<NotificationHistoryEntry>? EntryAdded;
    public event EventHandler? EntriesChanged;

    public NotificationHistoryEntry Add(NotificationHistoryEntry entry)
    {
        lock (sync)
        {
            entries.AddFirst(entry);
            while (entries.Count > MaxEntries)
            {
                entries.RemoveLast();
            }
        }

        EntryAdded?.Invoke(this, entry);
        return entry;
    }

    public void MarkRead(Guid id)
    {
        bool changed;
        lock (sync)
        {
            var entry = entries.FirstOrDefault(e => e.Id == id);
            changed = entry is { IsRead: false };
            if (entry != null)
            {
                entry.IsRead = true;
            }
        }

        if (changed)
        {
            EntriesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MarkAllRead()
    {
        bool changed;
        lock (sync)
        {
            changed = false;
            foreach (var entry in entries.Where(e => !e.IsRead))
            {
                entry.IsRead = true;
                changed = true;
            }
        }

        if (changed)
        {
            EntriesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Remove(Guid id)
    {
        bool changed;
        lock (sync)
        {
            var node = entries.First;
            changed = false;
            while (node != null)
            {
                if (node.Value.Id == id)
                {
                    entries.Remove(node);
                    changed = true;
                    break;
                }

                node = node.Next;
            }
        }

        if (changed)
        {
            EntriesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        bool changed;
        lock (sync)
        {
            changed = entries.Count > 0;
            entries.Clear();
        }

        if (changed)
        {
            EntriesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public NotificationHistoryEntry? Find(Guid id)
    {
        lock (sync)
        {
            return entries.FirstOrDefault(e => e.Id == id);
        }
    }
}
