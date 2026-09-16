using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// One category in the Set Categories dialog. <see cref="IsChecked"/> is null when only some of
/// the targeted models carry the category.
/// </summary>
public partial class ModelCategoryRowViewModel : ObservableObject
{
    public required string Name { get; init; }

    [ObservableProperty]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial bool? IsChecked { get; set; }

    [ObservableProperty]
    public partial Color Color { get; set; }
}
