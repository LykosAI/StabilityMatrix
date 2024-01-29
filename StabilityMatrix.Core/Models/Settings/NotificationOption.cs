using System.ComponentModel.DataAnnotations;

namespace StabilityMatrix.Core.Models.Settings;

public enum NotificationOption
{
    [Display(Name = "None", Description = "No notification")]
    None,

    [Display(Name = "In-App", Description = "Show a toast in the app")]
    AppToast,

    [Display(Name = "Desktop", Description = "Native desktop push notification")]
    NativePush
}
