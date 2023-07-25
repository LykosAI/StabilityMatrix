using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Services;

public class NotificationService : INotificationService
{
    private WindowNotificationManager? notificationManager;
    
    public void Initialize(
        Visual? visual, 
        NotificationPosition position = NotificationPosition.BottomRight,
        int maxItems = 3)
    {
        if (notificationManager is not null) return;
        notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(visual))
        {
            Position = position,
            MaxItems = maxItems
        };
    }

    public void Show(INotification notification)
    {
        notificationManager?.Show(notification);
    }

    public void Show(string title, string message,
        NotificationType appearance = NotificationType.Information)
    {
        Show(new Notification(title, message, appearance));
    }

    /// <inheritdoc />
    public async Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error)
    {
        try
        {
            return new TaskResult<T>(await task);
        }
        catch (Exception e)
        {
            Show(new Notification(title, message ?? e.Message, appearance));
            return TaskResult<T>.FromException(e);
        }
    }
    
    /// <inheritdoc />
    public async Task<TaskResult<bool>> TryAsync(
        Task task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error)
    {
        try
        {
            await task;
            return new TaskResult<bool>(true);
        }
        catch (Exception e)
        {
            Show(new Notification(title, message ?? e.Message, appearance));
            return new TaskResult<bool>(false, e);
        }
    }
}
