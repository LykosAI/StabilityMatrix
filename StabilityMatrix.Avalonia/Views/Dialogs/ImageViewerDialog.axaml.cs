using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Avalonia.ViewModels.Base;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class ImageViewerDialog : UserControl
{
    public ImageViewerDialog()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handle up/down presses for navigation
        base.OnKeyDown(e);
    }
}
