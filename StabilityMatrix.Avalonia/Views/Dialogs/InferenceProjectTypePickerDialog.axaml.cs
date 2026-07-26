using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<InferenceProjectTypePickerDialog>]
public partial class InferenceProjectTypePickerDialog : UserControlBase
{
    public InferenceProjectTypePickerDialog()
    {
        InitializeComponent();
    }
}
