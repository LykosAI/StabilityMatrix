using FluentAvalonia.UI.Controls;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(AnalyticsOptInDialog))]
[ManagedService]
[Transient]
public class AnalyticsOptInViewModel : ContentDialogViewModelBase
{
    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.IsFooterVisible = true;
        dialog.IsPrimaryButtonEnabled = true;
        dialog.PrimaryButtonText = "Don't Share Analytics";
        dialog.SecondaryButtonText = "Share Analytics";
        dialog.CloseOnClickOutside = false;
        dialog.DefaultButton = ContentDialogButton.Secondary;

        return dialog;
    }
}
