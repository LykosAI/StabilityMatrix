using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using DesktopNotifications.FreeDesktop;
using DesktopNotifications.Windows;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using INotificationManager = DesktopNotifications.INotificationManager;

namespace StabilityMatrix.Avalonia.Services;

[Singleton(typeof(INotificationService))]
public class NotificationService : INotificationService, IDisposable
{
    private readonly ILogger<NotificationService> logger;
    private readonly ISettingsManager settingsManager;

    private WindowNotificationManager? notificationManager;

    private readonly AsyncLock nativeNotificationManagerLock = new();
    private volatile INotificationManager? nativeNotificationManager;
    private volatile bool isNativeNotificationManagerInitialized;

    public NotificationService(ILogger<NotificationService> logger, ISettingsManager settingsManager)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
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

    /// <inheritdoc />
    public async Task ShowAsync(
        NotificationKey key,
        DesktopNotifications.Notification notification,
        TimeSpan? expiration = null
    )
    {
        // If settings has option preference, use that, otherwise default
        if (!settingsManager.Settings.NotificationOptions.TryGetValue(key, out var option))
        {
            option = key.DefaultOption;
        }

        switch (option)
        {
            case NotificationOption.None:
                break;
            case NotificationOption.NativePush:
            {
                // If native option is not supported, fallback to toast
                if (await GetNativeNotificationManagerAsync() is not { } nativeManager)
                {
                    Show(
                        new Notification(
                            notification.Title,
                            notification.Body,
                            NotificationType.Information,
                            expiration
                        )
                    );
                    return;
                }

                // Show native notification
                await nativeManager.ShowNotification(
                    notification,
                    expiration is null ? null : DateTimeOffset.Now.Add(expiration.Value)
                );
                break;
            }
            case NotificationOption.AppToast:
                // Show app toast
                Show(
                    new Notification(
                        notification.Title,
                        notification.Body,
                        NotificationType.Information,
                        expiration
                    )
                );
                break;
            default:
                logger.LogError("Unknown notification option {Option}", option);
                break;
        }
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

    public void Dispose()
    {
        nativeNotificationManager?.Dispose();

        GC.SuppressFinalize(this);
    }

    private async Task<INotificationManager?> GetNativeNotificationManagerAsync()
    {
        if (isNativeNotificationManagerInitialized)
            return nativeNotificationManager;

        using var _ = await nativeNotificationManagerLock.LockAsync();

        if (isNativeNotificationManagerInitialized)
            return nativeNotificationManager;

        try
        {
            if (Compat.IsWindows)
            {
                var context = WindowsApplicationContext.FromCurrentProcess();
                nativeNotificationManager = new WindowsNotificationManager(context);
            }
            else if (Compat.IsLinux)
            {
                var context = FreeDesktopApplicationContext.FromCurrentProcess();
                nativeNotificationManager = new FreeDesktopNotificationManager(context);
            }

            await nativeNotificationManager!.Initialize();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize native notification manager");
        }

        isNativeNotificationManagerInitialized = true;

        return nativeNotificationManager;
    }
}
