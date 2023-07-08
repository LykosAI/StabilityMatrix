using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.ViewModels;

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
}
