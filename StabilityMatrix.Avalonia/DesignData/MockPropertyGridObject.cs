using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PropertyModels.ComponentModel;
using StabilityMatrix.Avalonia.Languages;

#pragma warning disable CS0657 // Not a valid attribute location for this declaration

namespace StabilityMatrix.Avalonia.DesignData;

public partial class MockPropertyGridObject : ObservableObject
{
    [ObservableProperty]
    private string? stringProperty;

    [ObservableProperty]
    private int intProperty;

    [ObservableProperty]
    [property: Trackable(0, 50, Increment = 1, FormatString = "{0:0}")]
    private int intRange = 10;

    [ObservableProperty]
    [property: Trackable(0d, 1d, Increment = 0.01, FormatString = "{0:P0}")]
    private double floatPercentRange = 0.25;

    [ObservableProperty]
    [property: DisplayName("Int Custom Name")]
    private int intCustomNameProperty = 42;

    [ObservableProperty]
    [property: DisplayName(nameof(Resources.Label_Language))]
    private int? intLocalizedNameProperty;

    [ObservableProperty]
    private bool boolProperty;

    [ObservableProperty]
    [property: Category("Included Category")]
    private string? stringIncludedCategoryProperty;

    [ObservableProperty]
    [property: Category("Excluded Category")]
    private string? stringExcludedCategoryProperty;
}
