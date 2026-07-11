using System.Linq;
using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Helpers;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ModelPickerDialog>]
public partial class ModelPickerDialog : UserControlBase
{
    public ModelPickerDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, InputElement_OnKeyDown, handledEventsToo: true);
    }

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ModelPickerDialogViewModel viewModel)
            return;

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(PlatformKeyModifiers.CommandModifier))
        {
            SearchBox?.Focus();
            SearchBox?.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            viewModel.OnCloseButtonClick();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
            return;

        var modelToSelect = viewModel.SelectedModel ?? viewModel.FilteredModels.FirstOrDefault();
        if (modelToSelect is null)
            return;

        viewModel.SelectModelCommand.Execute(modelToSelect);
        e.Handled = true;
    }
}
