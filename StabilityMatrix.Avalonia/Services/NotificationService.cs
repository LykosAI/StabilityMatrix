using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(INotificationService))]
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> logger;
    private WindowNotificationManager? notificationManager;

    public NotificationService(ILogger<NotificationService> logger)
    {
        this.logger = logger;
    }

    public void Initialize(
        Visual? visual,
        NotificationPosition position = NotificationPosition.BottomRight,
        int maxItems = 4
    )
    {
        if (notificationManager is not null)
            return;
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

    public void Show(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information,
        TimeSpan? expiration = null
    )
    {
        Show(new Notification(title, message, appearance, expiration));
    }

    public void ShowPersistent(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information
    )
    {
        Show(new Notification(title, message, appearance, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public void ShowPersistent(
        AppException exception,
        NotificationType appearance = NotificationType.Warning,
        LogLevel logLevel = LogLevel.Warning
    )
    {
        // Log exception
        logger.Log(logLevel, exception, "{Message}", exception.Message);

        Show(new Notification(exception.Message, exception.Details, appearance, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public async Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error
    )
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
        NotificationType appearance = NotificationType.Error
    )
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
