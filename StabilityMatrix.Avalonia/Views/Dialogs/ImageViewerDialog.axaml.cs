using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[RegisterTransient<ImageViewerDialog>]
public partial class ImageViewerDialog : UserControlBase
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
        PressedMixin.Attach<Label>();
    }

    private void InfoButton_OnTapped(object? sender, TappedEventArgs e)
    {
        InfoTeachingTip.IsOpen ^= true;
    }
}
