using System;
using Avalonia.Controls.Notifications;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.Extensions;

public static class NotificationLevelExtensions
{
    public static NotificationType ToNotificationType(this NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Information => NotificationType.Information,
            NotificationLevel.Success => NotificationType.Success,
            NotificationLevel.Warning => NotificationType.Warning,
            NotificationLevel.Error => NotificationType.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
    }

    public static NotificationLevel ToNotificationLevel(this NotificationType type)
    {
        return type switch
        {
            NotificationType.Information => NotificationLevel.Information,
            NotificationType.Success => NotificationLevel.Success,
            NotificationType.Warning => NotificationLevel.Warning,
            NotificationType.Error => NotificationLevel.Error,
            _ => NotificationLevel.Information,
        };
    }
}
