using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

public partial class ImageViewerDialog : UserControl
{
    public static readonly StyledProperty<bool> IsFooterEnabledProperty = AvaloniaProperty.Register<
        ImageViewerDialog,
        bool
    >("IsFooterEnabled");

    /// <summary>
    /// Whether the footer with file name / size will be shown.
    /// </summary>
    public bool IsFooterEnabled
    {
        get => GetValue(IsFooterEnabledProperty);
        set => SetValue(IsFooterEnabledProperty, value);
    }

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
