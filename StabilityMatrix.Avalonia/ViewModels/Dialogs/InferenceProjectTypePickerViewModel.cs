using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Extensions;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.Views.Dialogs;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.ViewModels.Dialogs;

/// <summary>
/// Gallery picker shown when adding a new Inference tab. Lets the user choose a project
/// type from category-grouped cards and returns the choice via <see cref="SelectedType"/>.
/// </summary>
[View(typeof(InferenceProjectTypePickerDialog))]
[RegisterTransient<InferenceProjectTypePickerViewModel>]
[ManagedService]
public partial class InferenceProjectTypePickerViewModel : ContentDialogViewModelBase
{
    public override string? Title { get; set; } = "New Project";

    public IReadOnlyList<InferenceProjectTypeCategoryGroup> Categories { get; } =
        InferenceProjectTypeExtensions.GetGrouped();

    /// <summary>
    /// The type the user picked, or null if the dialog was dismissed. Read after the dialog
    /// returns <see cref="FluentAvalonia.UI.Controls.ContentDialogResult.Primary"/>.
    /// </summary>
    [ObservableProperty]
    private InferenceProjectType? selectedType;

    [RelayCommand]
    private void SelectType(InferenceProjectTypeInfo? info)
    {
        if (info is null)
            return;

        SelectedType = info.Type;
        OnPrimaryButtonClick();
    }

    public override BetterContentDialog GetDialog()
    {
        var dialog = base.GetDialog();

        dialog.MinDialogWidth = 600;
        dialog.MaxDialogWidth = 800;
        dialog.MinDialogHeight = 450;
        dialog.MaxDialogHeight = 720;
        dialog.IsFooterVisible = false;
        dialog.CloseOnClickOutside = true;

        return dialog;
    }
}
