using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using DesktopNotifications.FreeDesktop;
using DesktopNotifications.Windows;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Notifications;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;
using INotificationManager = DesktopNotifications.INotificationManager;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<INotificationService, NotificationService>]
public class NotificationService(
    ILogger<NotificationService> logger,
    ISettingsManager settingsManager,
    INotificationHistoryService historyService,
    INotificationActionDispatcher actionDispatcher
) : INotificationService, IDisposable
{
    private WindowNotificationManager? notificationManager;

    private readonly AsyncLock nativeNotificationManagerLock = new();
    private volatile INotificationManager? nativeNotificationManager;
    private volatile bool isNativeNotificationManagerInitialized;

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
            MaxItems = maxItems,
        };
    }

    /// <summary>
    /// Public entry point for raw INotification toasts (debug menu, ad-hoc callers that build
    /// their own Notification object). Writes a history entry derived from the notification and
    /// attaches our click handler if none was already set, so these flow into the Activity panel
    /// the same as keyed notifications.
    /// </summary>
    public void Show(INotification notification)
    {
        var entry = historyService.Add(
            new NotificationHistoryEntry
            {
                Title = notification.Title ?? string.Empty,
                Body = notification.Message,
                Level = notification.Type.ToNotificationLevel(),
            }
        );

        // Only attach our click handler if the caller hasn't set their own
        if (notification is Notification concrete && concrete.OnClick is null)
        {
            concrete.OnClick = () => OnToastClicked(entry.Id);
        }

        DispatchToWindowManager(notification);
    }

    /// <summary>Internal helper: send a toast straight to <see cref="WindowNotificationManager"/>
    /// without re-recording history. Used by paths that already wrote an entry.</summary>
    private void DispatchToWindowManager(INotification notification)
    {
        // Must marshal to UI thread - WindowNotificationManager requires it
        Dispatcher.UIThread.Invoke(() => notificationManager?.Show(notification));
    }

    /// <inheritdoc />
    public Task ShowPersistentAsync(
        NotificationKey key,
        DesktopNotifications.Notification notification,
        NotificationAction? action = null
    )
    {
        return ShowAsyncCore(key, notification, null, true, action);
    }

    /// <inheritdoc />
    public Task ShowAsync(
        NotificationKey key,
        DesktopNotifications.Notification notification,
        TimeSpan? expiration = null,
        NotificationAction? action = null
    )
    {
        // Use default expiration if not specified
        expiration ??= TimeSpan.FromSeconds(5);

        return ShowAsyncCore(key, notification, expiration, false, action);
    }

    private async Task ShowAsyncCore(
        NotificationKey key,
        DesktopNotifications.Notification notification,
        TimeSpan? expiration,
        bool isPersistent,
        NotificationAction? action
    )
    {
        // Always record to history, regardless of routing — users still want to see suppressed events.
        var entry = historyService.Add(
            new NotificationHistoryEntry
            {
                Key = key,
                Title = notification.Title ?? string.Empty,
                Body = notification.Body,
                BodyImagePath = notification.BodyImagePath,
                Level = key.Level,
                Action = action,
            }
        );

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
                    ShowToastFromEntry(entry, expiration, isPersistent);
                    return;
                }

                // Show native notification — native click is not wired in v1; entry is still in the activity panel.
                await nativeManager.ShowNotification(
                    notification,
                    expiration is null ? null : DateTimeOffset.Now.Add(expiration.Value)
                );

                break;
            }
            case NotificationOption.AppToast:
                ShowToastFromEntry(entry, expiration, isPersistent);
                break;
            default:
                logger.LogError("Unknown notification option {Option}", option);
                break;
        }
    }

    private void ShowToastFromEntry(NotificationHistoryEntry entry, TimeSpan? expiration, bool isPersistent)
    {
        var toast = new Notification(
            entry.Title,
            entry.Body ?? string.Empty,
            entry.Level.ToNotificationType(),
            isPersistent ? TimeSpan.Zero : expiration
        )
        {
            OnClick = () => OnToastClicked(entry.Id),
        };

        DispatchToWindowManager(toast);
    }

    private void OnToastClicked(Guid entryId)
    {
        historyService.MarkRead(entryId);
        var entry = historyService.Find(entryId);
        if (entry?.Action is { } action)
        {
            actionDispatcher.DispatchAsync(action).SafeFireAndForget();
        }
    }

    public void Show(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information,
        TimeSpan? expiration = null,
        NotificationAction? action = null
    )
    {
        var entry = historyService.Add(
            new NotificationHistoryEntry
            {
                Title = title,
                Body = message,
                Level = appearance.ToNotificationLevel(),
                Action = action,
            }
        );

        var toast = new Notification(title, message, appearance, expiration)
        {
            OnClick = () => OnToastClicked(entry.Id),
        };

        DispatchToWindowManager(toast);
    }

    public void ShowPersistent(
        string title,
        string message,
        NotificationType appearance = NotificationType.Information,
        NotificationAction? action = null
    )
    {
        var entry = historyService.Add(
            new NotificationHistoryEntry
            {
                Title = title,
                Body = message,
                Level = appearance.ToNotificationLevel(),
                Action = action,
            }
        );

        var toast = new Notification(title, message, appearance, TimeSpan.Zero)
        {
            OnClick = () => OnToastClicked(entry.Id),
        };

        DispatchToWindowManager(toast);
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

        ShowPersistent(exception.Message, exception.Details, appearance);
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
            Show(title, message ?? e.Message, appearance);
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
            logger.LogError(e, "{Exception}", e);
            Show(title, message ?? e.Message, appearance);
            return new TaskResult<bool>(false, e);
        }
    }

    public void Dispose()
    {
        nativeNotificationManager?.Dispose();

        GC.SuppressFinalize(this);
    }

    public async Task<INotificationManager?> GetNativeNotificationManagerAsync()
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
                var context = WindowsApplicationContext.FromCurrentProcess("Stability Matrix");
                nativeNotificationManager = new WindowsNotificationManager(context);

                await nativeNotificationManager.Initialize();
            }
            else if (Compat.IsLinux)
            {
                var context = FreeDesktopApplicationContext.FromCurrentProcess();
                nativeNotificationManager = new FreeDesktopNotificationManager(context);

                await nativeNotificationManager.Initialize();
            }
            else
            {
                logger.LogInformation("Native notifications are not supported on this platform");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to initialize native notification manager");
        }

        isNativeNotificationManagerInitialized = true;

        return nativeNotificationManager;
    }
}
