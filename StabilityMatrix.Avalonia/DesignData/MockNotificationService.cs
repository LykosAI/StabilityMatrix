using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockNotificationService : INotificationService
{
    public void Initialize(
        Visual? visual,
        NotificationPosition position = NotificationPosition.BottomRight,
        int maxItems = 3
    ) { }

    public void Show(INotification notification) { }

    public Task<TaskResult<T>> TryAsync<T>(
        Task<T> task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error
    )
    {
        return Task.FromResult(new TaskResult<T>(default!));
    }

    public Task<TaskResult<bool>> TryAsync(
        Task task,
        string title = "Error",
        string? message = null,
        NotificationType appearance = NotificationType.Error
    )
    {
        return Task.FromResult(new TaskResult<bool>(true));
    }

    public void Show(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information,
        TimeSpan? expiration = null
    ) { }

    public void ShowPersistent(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information
    ) { }

    /// <inheritdoc />
    public void ShowPersistent(
        AppException exception,
        NotificationType appearance = NotificationType.Error,
        LogLevel logLevel = LogLevel.Warning
    ) { }
}
