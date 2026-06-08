using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class NotificationHistoryServiceTests
{
    private static NotificationHistoryEntry MakeEntry(string title = "t", bool read = false) =>
        new()
        {
            Title = title,
            Body = "b",
            Level = NotificationLevel.Information,
            IsRead = read,
        };

    [TestMethod]
    public void Add_StoresEntriesNewestFirst()
    {
        var svc = new NotificationHistoryService();
        var a = svc.Add(MakeEntry("first"));
        var b = svc.Add(MakeEntry("second"));
        var c = svc.Add(MakeEntry("third"));

        var entries = svc.Entries.ToList();
        Assert.AreEqual(3, entries.Count);
        Assert.AreEqual(c.Id, entries[0].Id);
        Assert.AreEqual(b.Id, entries[1].Id);
        Assert.AreEqual(a.Id, entries[2].Id);
    }

    [TestMethod]
    public void Add_RaisesEntryAddedEvent()
    {
        var svc = new NotificationHistoryService();
        NotificationHistoryEntry? raised = null;
        svc.EntryAdded += (_, e) => raised = e;

        var entry = svc.Add(MakeEntry());

        Assert.IsNotNull(raised);
        Assert.AreEqual(entry.Id, raised!.Id);
    }

    [TestMethod]
    public void Add_EnforcesMaxEntriesCap()
    {
        var svc = new NotificationHistoryService();
        for (var i = 0; i < 150; i++)
        {
            svc.Add(MakeEntry($"e{i}"));
        }

        Assert.AreEqual(100, svc.Entries.Count);
        // Newest preserved
        Assert.AreEqual("e149", svc.Entries.First().Title);
        // Oldest evicted
        Assert.AreEqual("e50", svc.Entries.Last().Title);
    }

    [TestMethod]
    public void UnreadCount_TracksUnreadEntries()
    {
        var svc = new NotificationHistoryService();
        svc.Add(MakeEntry());
        svc.Add(MakeEntry());
        svc.Add(MakeEntry(read: true));

        Assert.AreEqual(2, svc.UnreadCount);
    }

    [TestMethod]
    public void MarkRead_FlipsEntryAndRaisesChange()
    {
        var svc = new NotificationHistoryService();
        var entry = svc.Add(MakeEntry());
        var raised = 0;
        svc.EntriesChanged += (_, _) => raised++;

        svc.MarkRead(entry.Id);

        Assert.IsTrue(svc.Find(entry.Id)!.IsRead);
        Assert.AreEqual(0, svc.UnreadCount);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void MarkRead_NoOpForAlreadyReadDoesNotRaise()
    {
        var svc = new NotificationHistoryService();
        var entry = svc.Add(MakeEntry(read: true));
        var raised = 0;
        svc.EntriesChanged += (_, _) => raised++;

        svc.MarkRead(entry.Id);

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void MarkAllRead_FlipsEveryUnreadEntry()
    {
        var svc = new NotificationHistoryService();
        svc.Add(MakeEntry());
        svc.Add(MakeEntry());
        svc.Add(MakeEntry(read: true));

        svc.MarkAllRead();

        Assert.AreEqual(0, svc.UnreadCount);
    }

    [TestMethod]
    public void Remove_DropsEntryAndRaisesChange()
    {
        var svc = new NotificationHistoryService();
        var a = svc.Add(MakeEntry("a"));
        var b = svc.Add(MakeEntry("b"));
        var raised = 0;
        svc.EntriesChanged += (_, _) => raised++;

        svc.Remove(a.Id);

        var remaining = svc.Entries.ToList();
        Assert.AreEqual(1, remaining.Count);
        Assert.AreEqual(b.Id, remaining[0].Id);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Clear_EmptiesEntriesAndRaisesChangeWhenNonEmpty()
    {
        var svc = new NotificationHistoryService();
        svc.Add(MakeEntry());
        svc.Add(MakeEntry());
        var raised = 0;
        svc.EntriesChanged += (_, _) => raised++;

        svc.Clear();

        Assert.AreEqual(0, svc.Entries.Count);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Clear_OnEmptyDoesNotRaise()
    {
        var svc = new NotificationHistoryService();
        var raised = 0;
        svc.EntriesChanged += (_, _) => raised++;

        svc.Clear();

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void Find_ReturnsEntryById()
    {
        var svc = new NotificationHistoryService();
        var entry = svc.Add(MakeEntry("findme"));

        var found = svc.Find(entry.Id);

        Assert.IsNotNull(found);
        Assert.AreEqual("findme", found!.Title);
    }

    [TestMethod]
    public void Find_ReturnsNullWhenMissing()
    {
        var svc = new NotificationHistoryService();

        Assert.IsNull(svc.Find(Guid.NewGuid()));
    }

    [TestMethod]
    public void Entry_CarriesNotificationAction()
    {
        var svc = new NotificationHistoryService();
        var action = new OpenFolderAction(@"C:\some\path");

        var entry = svc.Add(
            new NotificationHistoryEntry
            {
                Title = "t",
                Action = action,
                Level = NotificationLevel.Success,
            }
        );

        Assert.IsInstanceOfType<OpenFolderAction>(svc.Find(entry.Id)!.Action);
        Assert.AreEqual(@"C:\some\path", ((OpenFolderAction)svc.Find(entry.Id)!.Action!).Path);
    }
}
