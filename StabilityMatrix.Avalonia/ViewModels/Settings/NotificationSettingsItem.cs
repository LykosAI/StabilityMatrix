using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Settings;

[Transient]
[ManagedService]
public partial class NotificationSettingsItem(ISettingsManager settingsManager) : ObservableObject
{
    public NotificationKey? Key { get; set; }

    [ObservableProperty]
    private NotificationOption? option;

    public static IEnumerable<NotificationOption> AvailableOptions => Enum.GetValues<NotificationOption>();

    partial void OnOptionChanged(NotificationOption? oldValue, NotificationOption? newValue)
    {
        if (Key is null || oldValue is null || newValue is null)
            return;

        settingsManager.Transaction(settings =>
        {
            settings.NotificationOptions[Key] = newValue.Value;
        });
    }
}
