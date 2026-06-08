using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Progress;

public partial class NotificationItemViewModel : ViewModelBase
{
    private readonly INotificationHistoryService historyService;
    private readonly INotificationActionDispatcher dispatcher;

    public NotificationHistoryEntry Entry { get; }

    public Guid Id => Entry.Id;
    public string Title => Entry.Title;
    public string? Body => Entry.Body;
    public string? BodyImagePath => Entry.BodyImagePath;
    public bool HasBodyImage => !string.IsNullOrEmpty(Entry.BodyImagePath);
    public bool HasBody => !string.IsNullOrEmpty(Entry.Body);
    public DateTimeOffset Timestamp => Entry.Timestamp;
    public NotificationType SeverityType => Entry.Level.ToNotificationType();

    public IBrush SeverityBrush =>
        Entry.Level switch
        {
            Core.Models.Settings.NotificationLevel.Success => Brushes.MediumSeaGreen,
            Core.Models.Settings.NotificationLevel.Warning => Brushes.Orange,
            Core.Models.Settings.NotificationLevel.Error => Brushes.IndianRed,
            _ => Brushes.SteelBlue,
        };

    public bool HasAction => Entry.Action is not null;

    public string ActionLabel =>
        Entry.Action switch
        {
            OpenFolderAction => Resources.Action_OpenFolder,
            NavigateToPageAction => Resources.Action_Open,
            ToggleProgressFlyoutAction => Resources.Action_ShowActivity,
            _ => Resources.Action_Open,
        };

    public string FormattedTimestamp => FormatRelative(Entry.Timestamp);

    /// <summary>True only when there is a body to show AND the row is currently collapsed —
    /// keeps the preview from duplicating the body shown in the expanded section.</summary>
    public bool IsPreviewBodyVisible => HasBody && !IsExpanded;

    /// <summary>Drives the leading unread-state dot. Disappears once the entry is read.</summary>
    public bool IsUnreadIndicatorVisible => !IsRead;

    /// <summary>Read entries fade slightly so the active ones pop visually.</summary>
    public double ReadOpacity => IsRead ? 0.55 : 1.0;

    [ObservableProperty]
    private bool isExpanded;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(IsPreviewBodyVisible));

    partial void OnIsReadChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUnreadIndicatorVisible));
        OnPropertyChanged(nameof(ReadOpacity));
    }

    [ObservableProperty]
    private bool isRead;

    public NotificationItemViewModel(
        NotificationHistoryEntry entry,
        INotificationHistoryService historyService,
        INotificationActionDispatcher dispatcher
    )
    {
        Entry = entry;
        this.historyService = historyService;
        this.dispatcher = dispatcher;
        isRead = entry.IsRead;
    }

    [RelayCommand]
    private async Task InvokeActionAsync()
    {
        MarkRead();
        if (Entry.Action is { } action)
        {
            await dispatcher.DispatchAsync(action);
        }
    }

    [RelayCommand]
    private void Dismiss() => historyService.Remove(Entry.Id);

    [RelayCommand]
    private void ToggleDetails()
    {
        IsExpanded = !IsExpanded;
        MarkRead();
    }

    public void MarkRead()
    {
        if (IsRead)
            return;
        historyService.MarkRead(Entry.Id);
        IsRead = true;
    }

    public void RefreshReadState() => IsRead = Entry.IsRead;

    private static string FormatRelative(DateTimeOffset ts)
    {
        var delta = DateTimeOffset.Now - ts;
        if (delta < TimeSpan.FromSeconds(45))
            return Resources.Label_RelativeTime_JustNow;
        if (delta < TimeSpan.FromMinutes(60))
            return string.Format(Resources.Label_RelativeTime_MinutesAgo, (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromHours(24) && ts.Date == DateTimeOffset.Now.Date)
            return ts.ToLocalTime().ToString("t");
        return ts.ToLocalTime().ToString("g");
    }
}
