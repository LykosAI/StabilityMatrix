using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.PropertyGrid.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using OneOf;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(PropertyGridDialog))]
[ManagedService]
[Transient]
public partial class PropertyGridViewModel : TaskDialogViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedObjectItemsSource))]
    private OneOf<INotifyPropertyChanged, IEnumerable<INotifyPropertyChanged>>? selectedObject;

    public IEnumerable<INotifyPropertyChanged>? SelectedObjectItemsSource =>
        SelectedObject?.Match(single => [single], multiple => multiple);

    [ObservableProperty]
    private PropertyGridShowStyle showStyle = PropertyGridShowStyle.Alphabetic;

    [ObservableProperty]
    private IReadOnlyList<string>? excludeCategories;

    [ObservableProperty]
    private IReadOnlyList<string>? includeCategories;
}
