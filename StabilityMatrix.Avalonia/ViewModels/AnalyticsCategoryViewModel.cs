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
public partial class AnalyticsCategoryViewModel(AnalyticsCategoryInfo info) : ObservableObject
{
    public AnalyticsCategory Category => info.Category;

    public string Title => info.Title;

    public string Description => info.Description;

    public IconSource IconSource { get; } =
        new SymbolIconSource { Symbol = info.Icon, IconVariant = IconVariant.Filled };

    public TextDocument ExampleDocument { get; } = new(info.ExampleJson);

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}
