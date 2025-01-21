using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;

namespace StabilityMatrix.Avalonia.Controls;

public class StarsRating : TemplatedControlBase
{
    private SymbolIcon? StarFilledIcon => Resources["StarFilledIcon"] as SymbolIcon;

    private ItemsControl? itemsControl;

    private IEnumerable<SymbolIcon> StarItems => itemsControl!.ItemsSource!.Cast<SymbolIcon>();

    public static readonly StyledProperty<bool> IsEditableProperty = AvaloniaProperty.Register<
        StarsRating,
        bool
    >("IsEditable");

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public static readonly StyledProperty<int> MaximumProperty = AvaloniaProperty.Register<StarsRating, int>(
        nameof(Maximum),
        5
    );

    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<
        StarsRating,
        double
    >(nameof(Value));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        itemsControl = e.NameScope.Find<ItemsControl>("PART_StarsItemsControl")!;

        CreateStars();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        if (change.Property == ValueProperty || change.Property == MaximumProperty)
        {
            SyncStarState();
        }
    }

    private void CreateStars()
    {
        if (itemsControl is null)
        {
            return;
        }

        // Fill stars
        var stars = new List<Control>();

        for (var i = 0; i < Maximum; i++)
        {
            var star = new SymbolIcon
            {
                FontSize = FontSize,
                Margin = new Thickness(0, 0),
                Symbol = Symbol.Star,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = i
            };

            stars.Add(star);
            OnStarAdded(star);
        }

        itemsControl.ItemsSource = stars;
        SyncStarState();
    }

    private void OnStarAdded(SymbolIcon item)
    {
        if (IsEditable)
        {
            item.Tapped += (sender, args) =>
            {
                var star = (SymbolIcon)sender!;
                Value = (int)star.Tag! + 1;
            };
        }
    }

    /// <summary>
    /// Round a number to the nearest 0.5
    /// </summary>
    private static double RoundToHalf(double value)
    {
        return Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
    }

    private void SyncStarState()
    {
        // Set star to filled when Value is greater than or equal to the star index
        foreach (var star in StarItems)
        {
            // Add 1 to tag since its index is 0-based
            var tag = (int)star.Tag! + 1;

            // Fill if current is equal or lower than floor of Value
            if (tag <= Math.Floor(RoundToHalf(Value)))
            {
                star.Symbol = Symbol.Star;
                star.IconVariant = IconVariant.Filled;
                star.Foreground = Foreground;
            }
            // If current is between floor and ceil of value, use half-star
            else if (tag <= Math.Ceiling(RoundToHalf(Value)))
            {
                star.Symbol = Symbol.StarHalf;
                star.IconVariant = IconVariant.Filled;
                star.Foreground = Foreground;
            }
            // Otherwise no fill and gray disabled color
            else
            {
                star.Symbol = Symbol.Star;
                star.IconVariant = IconVariant.Regular;
                star.Foreground = new SolidColorBrush(Colors.DarkSlateGray);
            }
        }
    }
}
