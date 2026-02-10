using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ImageAnnotationEditorDialog>]
public partial class ImageAnnotationEditorDialog : UserControlBase
{
    public ImageAnnotationEditorDialog()
    {
        InitializeComponent();
    }
}
