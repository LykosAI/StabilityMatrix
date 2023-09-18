using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Views.Dialogs;

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
        var infoTip = InfoTeachingTip;
        infoTip.IsOpen = true;
    }
}
