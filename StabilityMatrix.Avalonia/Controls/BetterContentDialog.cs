using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class BetterContentDialog : ContentDialog
{
    protected override Type StyleKeyOverride { get; } = typeof(ContentDialog);

    public static readonly StyledProperty<bool> IsFooterVisibleProperty = AvaloniaProperty.Register<BetterContentDialog, bool>(
        "IsFooterVisible", true);

    public bool IsFooterVisible
    {
        get => GetValue(IsFooterVisibleProperty);
        set => SetValue(IsFooterVisibleProperty, value);
    }

    public BetterContentDialog()
    {
        AddHandler(LoadedEvent, OnLoaded);
    }

    private void OnLoaded(object? sender, RoutedEventArgs? e)
    {
        // Check if we need to hide the footer
        if (IsFooterVisible) return;
        
        // Find the named grid
        // https://github.com/amwx/FluentAvalonia/blob/master/src/FluentAvalonia/Styling/
        // ControlThemes/FAControls/ContentDialogStyles.axaml#L96

        var border = VisualChildren[0] as Border;
        var panel = border?.Child as Panel;
        var faBorder = panel?.Children[0] as FABorder;
        var border2 = faBorder?.Child as Border;
        var grid = border2?.Child as Grid;

        // Get the parent border, which is what we want to hide
        if (grid?.Children[1] is not Border actualBorder)
        {
            throw new InvalidOperationException("Could not find parent border");
        }
        // Hide the border
        actualBorder.IsVisible = false;
    }
}
