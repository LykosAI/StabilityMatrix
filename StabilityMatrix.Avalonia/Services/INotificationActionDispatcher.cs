using StabilityMatrix.Core.Models.Notifications;

namespace StabilityMatrix.Avalonia.Services;

/// <summary>
/// Performs the side effect described by a <see cref="NotificationAction"/> when the user clicks a toast or
/// activity-panel entry.
/// </summary>
public interface INotificationActionDispatcher
{
    Task DispatchAsync(NotificationAction action);
}
