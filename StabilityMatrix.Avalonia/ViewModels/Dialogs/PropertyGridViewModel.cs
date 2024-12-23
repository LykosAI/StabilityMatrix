using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.PropertyGrid.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using OneOf;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

[View(typeof(PropertyGridDialog))]
[ManagedService]
[RegisterTransient<PropertyGridViewModel>]
public partial class PropertyGridViewModel : ContentDialogViewModelBase
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

    /// <inheritdoc />
    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.Padding = new Thickness(0);
        dialog.CloseOnClickOutside = true;
        dialog.CloseButtonText = Resources.Action_Close;

        return dialog;
    }

    /// <summary>
    /// Like <see cref="GetDialog"/>, but with a primary save button.
    /// </summary>
    public BetterContentDialog GetSaveDialog()
    {
        var dialog = base.GetDialog();

        dialog.Padding = new Thickness(0);
        dialog.CloseOnClickOutside = true;
        dialog.CloseButtonText = Resources.Action_Close;
        dialog.PrimaryButtonText = Resources.Action_Save;
        dialog.DefaultButton = ContentDialogButton.Primary;

        return dialog;
    }
}
