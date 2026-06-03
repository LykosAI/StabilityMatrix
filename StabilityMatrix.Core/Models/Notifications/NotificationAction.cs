using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Notifications;

/// <summary>
/// Discriminated record describing what should happen when a notification (toast or history entry) is invoked.
/// New variants must be registered with <see cref="JsonDerivedTypeAttribute"/> here so the dispatcher can switch on them.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(OpenFolderAction), "OpenFolder")]
[JsonDerivedType(typeof(NavigateToPageAction), "NavigateToPage")]
[JsonDerivedType(typeof(ToggleProgressFlyoutAction), "ToggleProgressFlyout")]
public abstract record NotificationAction;

/// <summary>Open the given filesystem path in the OS file manager.</summary>
public sealed record OpenFolderAction(string Path) : NotificationAction;

/// <summary>
/// Navigate the main shell to the page identified by the given ViewModel type.
/// Stored as the assembly-qualified type name so the action remains serializable.
/// </summary>
public sealed record NavigateToPageAction(string PageTypeName) : NotificationAction;

/// <summary>Open the activity flyout in the sidebar footer.</summary>
public sealed record ToggleProgressFlyoutAction : NotificationAction;
