using Avalonia;
using Avalonia.Input;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

[Transient]
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
    }

    private void InfoButton_OnTapped(object? sender, TappedEventArgs e)
    {
        InfoTeachingTip.IsOpen ^= true;
    }
}
