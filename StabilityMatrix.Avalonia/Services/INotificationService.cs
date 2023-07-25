using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Services;

public interface INotificationService
{
    public void Initialize(
        Visual? visual,
        NotificationPosition position = NotificationPosition.BottomRight,
        int maxItems = 3);

    public void Show(INotification notification);

    /// <summary>
    /// Attempt to run the given task, showing a generic error notification if it fails.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <param name="title">The title to show in the notification.</param>
    /// <param name="message">The message to show, default to exception.Message</param>
    /// <param name="appearance">The appearance of the notification.</param>
    Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error);

    /// <summary>
    /// Attempt to run the given void task, showing a generic error notification if it fails.
    /// Return a TaskResult with true if the task succeeded, false if it failed.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <param name="title">The title to show in the notification.</param>
    /// <param name="message">The message to show, default to exception.Message</param>
    /// <param name="appearance">The appearance of the notification.</param>
    Task<TaskResult<bool>> TryAsync(
        Task task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error);

    /// <summary>
    /// Show a notification with the given parameters.
    /// </summary>
    void Show(
        string title, 
        string message,
        NotificationType appearance = NotificationType.Information,
        TimeSpan? expiration = null);

    /// <summary>
    /// Show a notification that will not auto-dismiss.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="appearance"></param>
    void ShowPersistent(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information);
}
