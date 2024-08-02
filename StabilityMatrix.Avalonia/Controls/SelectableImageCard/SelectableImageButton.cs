using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace StabilityMatrix.Avalonia.Controls.SelectableImageCard;

public class SelectableImageButton : Button
{
    public static readonly StyledProperty<bool?> IsSelectedProperty =
        ToggleButton.IsCheckedProperty.AddOwner<SelectableImageButton>();

    public static readonly StyledProperty<Uri?> SourceProperty = AvaloniaProperty.Register<
        SelectableImageButton,
        Uri?
    >("Source");

    public static readonly StyledProperty<double> ImageWidthProperty = AvaloniaProperty.Register<
        SelectableImageButton,
        double
    >("ImageWidth", 300);

    public static readonly StyledProperty<double> ImageHeightProperty = AvaloniaProperty.Register<
        SelectableImageButton,
        double
    >("ImageHeight", 300);

    static SelectableImageButton()
    {
        AffectsRender<SelectableImageButton>(ImageWidthProperty, ImageHeightProperty);
        AffectsArrange<SelectableImageButton>(ImageWidthProperty, ImageHeightProperty);
    }

    public double ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public double ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public bool? IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
