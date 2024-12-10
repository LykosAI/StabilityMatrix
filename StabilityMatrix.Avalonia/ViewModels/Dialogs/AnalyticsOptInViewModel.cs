using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using Injectio.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(AnalyticsOptInDialog))]
[ManagedService]
[RegisterTransient<AnalyticsOptInViewModel>]
public class AnalyticsOptInViewModel : ContentDialogViewModelBase
{
    public string ChangeThisBehaviorInSettings =>
        string.Format(Resources.TextTemplate_YouCanChangeThisBehavior, "Settings > System > Analytics")
            .Trim();

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.IsPrimaryButtonEnabled = true;
        dialog.PrimaryButtonText = "Don't Share Analytics";
        dialog.SecondaryButtonText = "Share Analytics";
        dialog.CloseOnClickOutside = false;
        dialog.DefaultButton = ContentDialogButton.Secondary;

        return dialog;
    }
}
