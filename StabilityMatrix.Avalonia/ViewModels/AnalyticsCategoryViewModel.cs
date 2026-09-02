using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Models.Analytics;
using StabilityMatrix.Core.Models.Settings;
using SymbolIconSource = FluentIcons.Avalonia.Fluent.SymbolIconSource;

namespace StabilityMatrix.Avalonia.ViewModels;

/// <summary>
/// One analytics category as shown in the consent dialog and on the settings page: its
/// metadata plus a toggle. Owners decide what a toggle change means (a local choice in the
/// dialog, a saved setting on the settings page).
/// </summary>
public partial class AnalyticsCategoryViewModel : ObservableObject
{
    private readonly AnalyticsCategoryInfo info;
    private readonly TextDocument detailedExample;
    private readonly TextDocument? minimalExample;

    public AnalyticsCategoryViewModel(AnalyticsCategoryInfo info)
    {
        this.info = info;
        detailedExample = new TextDocument(info.ExampleJson);
        minimalExample = info.MinimalExampleJson is { } minimal ? new TextDocument(minimal) : null;
        IconSource = new SymbolIconSource { Symbol = info.Icon, IconVariant = IconVariant.Filled };
    }

    public AnalyticsCategory Category => info.Category;

    public string Title => info.Title;

    public string Description => info.Description;

    public IconSource IconSource { get; }

    /// <summary>Whether the category offers a minimal detail level (hardware does).</summary>
    public bool SupportsDetailLevel => minimalExample is not null;

    /// <summary>Example of what the category sends at the currently selected detail level.</summary>
    public TextDocument ExampleDocument =>
        IsDetailed || minimalExample is null ? detailedExample : minimalExample;

    public HardwareDetailLevel DetailLevel =>
        IsDetailed ? HardwareDetailLevel.Detailed : HardwareDetailLevel.Minimal;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExampleDocument), nameof(DetailLevel))]
    public partial bool IsDetailed { get; set; } = true;
}
