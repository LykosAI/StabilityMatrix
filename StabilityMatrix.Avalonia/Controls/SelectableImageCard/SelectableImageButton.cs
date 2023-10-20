using Avalonia;
using Avalonia.Controls;

namespace StabilityMatrix.Avalonia.Controls.SelectableImageCard;

public class SelectableImageButton : Button
{
    public static readonly StyledProperty<bool?> IsSelectedProperty =
        CheckBox.IsCheckedProperty.AddOwner<SelectableImageButton>();

    public static readonly StyledProperty<string?> SourceProperty =
        BetterAdvancedImage.SourceProperty.AddOwner<SelectableImageButton>();

    public bool? IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
