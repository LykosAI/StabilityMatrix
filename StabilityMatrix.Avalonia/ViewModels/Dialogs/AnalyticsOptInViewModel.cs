using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Analytics;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper.Analytics;
using StabilityMatrix.Core.Models.Settings;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(AnalyticsOptInDialog))]
[ManagedService]
[RegisterTransient<AnalyticsOptInViewModel>]
public partial class AnalyticsOptInViewModel : ContentDialogViewModelBase
{
    /// <summary>All categories, pre-selected; the user unticks what they don't want to share.</summary>
    public IReadOnlyList<AnalyticsCategoryViewModel> Categories { get; } =
        AnalyticsCategories
            .All.Select(info => new AnalyticsCategoryViewModel(info) { IsEnabled = true })
            .ToList();

    /// <summary>The category whose example document is being previewed.</summary>
    [ObservableProperty]
    public partial AnalyticsCategoryViewModel? SelectedCategory { get; set; }

    /// <summary>
    /// True when shown to a user who already opted in under an older consent revision, so the
    /// dialog explains that the offered categories changed.
    /// </summary>
    [ObservableProperty]
    public partial bool IsConsentUpdate { get; set; }

    public string ChangeThisBehaviorInSettings =>
        string.Format(Resources.TextTemplate_YouCanChangeThisBehavior, "Settings > System > Analytics")
            .Trim();

    public AnalyticsOptInViewModel()
    {
        SelectedCategory = Categories.FirstOrDefault();
    }

    /// <summary>Snapshot of the dialog's choices to hand to the analytics helper.</summary>
    public AnalyticsConsentChoice GetConsentChoice(bool accepted)
    {
        var categories = Categories.Where(c => c.IsEnabled).Select(c => c.Category).ToList();
        var hardware = Categories.FirstOrDefault(c => c.Category == AnalyticsCategory.Hardware);

        return new AnalyticsConsentChoice(
            accepted,
            categories,
            hardware?.DetailLevel ?? HardwareDetailLevel.Detailed
        );
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();
        dialog.IsPrimaryButtonEnabled = true;
        dialog.PrimaryButtonText = "Don't Share Analytics";
        dialog.SecondaryButtonText = "Share Analytics";
        dialog.CloseOnClickOutside = false;
        dialog.DefaultButton = ContentDialogButton.Secondary;
        dialog.MaxDialogWidth = 700;

        return dialog;
    }
}
