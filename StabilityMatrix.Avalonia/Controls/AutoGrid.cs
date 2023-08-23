// Modified from https://github.com/AvaloniaUI/AvaloniaAutoGrid
/*The MIT License (MIT)

Copyright (c) 2013 Charles Brown (carbonrobot)

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.*/

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace StabilityMatrix.Avalonia.Controls;

/// <summary>
/// Defines a flexible grid area that consists of columns and rows.
/// Depending on the orientation, either the rows or the columns are auto-generated,
/// and the children's position is set according to their index.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class AutoGrid : Grid
{
    /// <summary>
    /// Gets or sets the child horizontal alignment.
    /// </summary>
    /// <value>The child horizontal alignment.</value>
    [Category("Layout"), Description("Presets the horizontal alignment of all child controls")]
    public HorizontalAlignment? ChildHorizontalAlignment
    {
        get => (HorizontalAlignment?)GetValue(ChildHorizontalAlignmentProperty);
        set => SetValue(ChildHorizontalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the child margin.
    /// </summary>
    /// <value>The child margin.</value>
    [Category("Layout"), Description("Presets the margin of all child controls")]
    public Thickness? ChildMargin
    {
        get => (Thickness?)GetValue(ChildMarginProperty);
        set => SetValue(ChildMarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the child vertical alignment.
    /// </summary>
    /// <value>The child vertical alignment.</value>
    [Category("Layout"), Description("Presets the vertical alignment of all child controls")]
    public VerticalAlignment? ChildVerticalAlignment
    {
        get => (VerticalAlignment?)GetValue(ChildVerticalAlignmentProperty);
        set => SetValue(ChildVerticalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the column count
    /// </summary>
    [Category("Layout"), Description("Defines a set number of columns")]
    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty)!;
        set => SetValue(ColumnCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the fixed column width
    /// </summary>
    [Category("Layout"), Description("Presets the width of all columns set using the ColumnCount property")]

    public GridLength ColumnWidth
    {
        get => (GridLength)GetValue(ColumnWidthProperty)!;
        set => SetValue(ColumnWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the children are automatically indexed.
    /// <remarks>
    /// The default is <c>true</c>.
    /// Note that if children are already indexed, setting this property to <c>false</c> will not remove their indices.
    /// </remarks>
    /// </summary>
    [Category("Layout"), Description("Set to false to disable the auto layout functionality")]
    public bool IsAutoIndexing
    {
        get => (bool)GetValue(IsAutoIndexingProperty)!;
        set => SetValue(IsAutoIndexingProperty, value);
    }

    /// <summary>
    /// Gets or sets the orientation.
    /// <remarks>The default is Vertical.</remarks>
    /// </summary>
    /// <value>The orientation.</value>
    [Category("Layout"), Description("Defines the directionality of the autolayout. Use vertical for a column first layout, horizontal for a row first layout.")]
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of rows
    /// </summary>
    [Category("Layout"), Description("Defines a set number of rows")]
    public int RowCount
    {
        get => (int)GetValue(RowCountProperty)!;
        set => SetValue(RowCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the fixed row height
    /// </summary>
    [Category("Layout"), Description("Presets the height of all rows set using the RowCount property")]
    public GridLength RowHeight
    {
        get => (GridLength)GetValue(RowHeightProperty)!;
        set => SetValue(RowHeightProperty, value);
    }
        
    /// <summary>
    /// Handles the column count changed event
    /// </summary>
    public static void ColumnCountChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if ((int)e.NewValue! < 0)
            return;

        var grid = (AutoGrid)e.Sender;

            
        // look for an existing column definition for the height
        var width = grid.ColumnWidth;
        if (!grid.IsSet(ColumnWidthProperty) && grid.ColumnDefinitions.Count > 0)
            width = grid.ColumnDefinitions[0].Width;

        // clear and rebuild
        grid.ColumnDefinitions.Clear();
        for (var i = 0; i < (int)e.NewValue; i++)
            grid.ColumnDefinitions.Add(
                new ColumnDefinition() { Width = width });
    }

    /// <summary>
    /// Handle the fixed column width changed event
    /// </summary>
    public static void FixedColumnWidthChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var grid = (AutoGrid)e.Sender;

        // add a default column if missing
        if (grid.ColumnDefinitions.Count == 0)
            grid.ColumnDefinitions.Add(new ColumnDefinition());

        // set all existing columns to this width
        foreach (var t in grid.ColumnDefinitions)
            t.Width = (GridLength)e.NewValue!;
    }

    /// <summary>
    /// Handle the fixed row height changed event
    /// </summary>
    public static void FixedRowHeightChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var grid = (AutoGrid)e.Sender;

        // add a default row if missing
        if (grid.RowDefinitions.Count == 0)
            grid.RowDefinitions.Add(new RowDefinition());

        // set all existing rows to this height
        foreach (var t in grid.RowDefinitions)
            t.Height = (GridLength)e.NewValue!;
    }

    /// <summary>
    /// Handles the row count changed event
    /// </summary>
    public static void RowCountChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if ((int)e.NewValue! < 0)
            return;

        var grid = (AutoGrid)e.Sender;

        // look for an existing row to get the height
        var height = grid.RowHeight;
        if (!grid.IsSet(RowHeightProperty) && grid.RowDefinitions.Count > 0)
            height = grid.RowDefinitions[0].Height;

        // clear and rebuild
        grid.RowDefinitions.Clear();
        for (var i = 0; i < (int)e.NewValue; i++)
            grid.RowDefinitions.Add(
                new RowDefinition() { Height = height });
    }

    /// <summary>
    /// Called when [child horizontal alignment changed].
    /// </summary>
    private static void OnChildHorizontalAlignmentChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var grid = (AutoGrid)e.Sender;
        foreach (var child in grid.Children)
        {
            child.SetValue(HorizontalAlignmentProperty,
                grid.ChildHorizontalAlignment ?? AvaloniaProperty.UnsetValue);
        }
    }

    /// <summary>
    /// Called when [child layout changed].
    /// </summary>
    private static void OnChildMarginChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var grid = (AutoGrid)e.Sender;
        foreach (var child in grid.Children)
        {
            child.SetValue(MarginProperty, grid.ChildMargin ?? AvaloniaProperty.UnsetValue);
        }
    }

    /// <summary>
    /// Called when [child vertical alignment changed].
    /// </summary>
    private static void OnChildVerticalAlignmentChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var grid = (AutoGrid)e.Sender;
        foreach (var child in grid.Children)
        {
            child.SetValue(VerticalAlignmentProperty, grid.ChildVerticalAlignment ?? AvaloniaProperty.UnsetValue);
        }
    }

    /// <summary>
    /// Apply child margins and layout effects such as alignment
    /// </summary>
    private void ApplyChildLayout(Control child)
    {
        if (ChildMargin != null)
        {
            child.SetValue(MarginProperty, ChildMargin.Value, BindingPriority.Template);
        }
        if (ChildHorizontalAlignment != null)
        {
            child.SetValue(HorizontalAlignmentProperty, ChildHorizontalAlignment.Value, BindingPriority.Template);
        }
        if (ChildVerticalAlignment != null)
        {
            child.SetValue(VerticalAlignmentProperty, ChildVerticalAlignment.Value, BindingPriority.Template);
        }
    }

    /// <summary>
    /// Clamp a value to its maximum.
    /// </summary>
    private int Clamp(int value, int max)
    {
        return (value > max) ? max : value;
    }

    /// <summary>
    /// Perform the grid layout of row and column indexes
    /// </summary>
    private void PerformLayout()
    {
        var fillRowFirst = Orientation == Orientation.Horizontal;
        var rowCount = RowDefinitions.Count;
        var colCount = ColumnDefinitions.Count;

        if (rowCount == 0 || colCount == 0)
            return;

        var position = 0;
        var skip = new bool[rowCount, colCount];
        foreach (var child in Children.OfType<Control>())
        {
            var childIsCollapsed = !child.IsVisible;
            if (IsAutoIndexing && !childIsCollapsed)
            {
                if (fillRowFirst)
                {
                    var row = Clamp(position / colCount, rowCount - 1);
                    var col = Clamp(position % colCount, colCount - 1);
                    if (skip[row, col])
                    {
                        position++;
                        row = (position / colCount);
                        col = (position % colCount);
                    }

                    SetRow(child, row);
                    SetColumn(child, col);
                    position += GetColumnSpan(child);

                    var offset = GetRowSpan(child) - 1;
                    while (offset > 0)
                    {
                        skip[row + offset--, col] = true;
                    }
                }
                else
                {
                    var row = Clamp(position % rowCount, rowCount - 1);
                    var col = Clamp(position / rowCount, colCount - 1);
                    if (skip[row, col])
                    {
                        position++;
                        row = position % rowCount;
                        col = position / rowCount;
                    }

                    SetRow(child, row);
                    SetColumn(child, col);
                    position += GetRowSpan(child);

                    var offset = GetColumnSpan(child) - 1;
                    while (offset > 0)
                    {
                        skip[row, col + offset--] = true;
                    }
                }
            }

            ApplyChildLayout(child);
        }
    }

    public static readonly AvaloniaProperty<HorizontalAlignment?> ChildHorizontalAlignmentProperty =
        AvaloniaProperty.Register<AutoGrid, HorizontalAlignment?>("ChildHorizontalAlignment");

    public static readonly AvaloniaProperty<Thickness?> ChildMarginProperty =
        AvaloniaProperty.Register<AutoGrid, Thickness?>("ChildMargin");

    public static readonly AvaloniaProperty<VerticalAlignment?> ChildVerticalAlignmentProperty =
        AvaloniaProperty.Register<AutoGrid, VerticalAlignment?>("ChildVerticalAlignment");

    public static readonly AvaloniaProperty<int> ColumnCountProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("ColumnCount", typeof(AutoGrid), 1);

    public static readonly AvaloniaProperty<GridLength> ColumnWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, GridLength>("ColumnWidth", typeof(AutoGrid), GridLength.Auto);

    public static readonly AvaloniaProperty<bool> IsAutoIndexingProperty =
        AvaloniaProperty.Register<AutoGrid, bool>("IsAutoIndexing", true);

    public static readonly AvaloniaProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<AutoGrid, Orientation>("Orientation", Orientation.Vertical);

    public static readonly AvaloniaProperty<int> RowCountProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("RowCount", typeof(AutoGrid), 1);

    public static readonly AvaloniaProperty<GridLength> RowHeightProperty =
        AvaloniaProperty.RegisterAttached<Control, GridLength>("RowHeight", typeof(AutoGrid), GridLength.Auto);

    static AutoGrid()
    {
        AffectsMeasure<AutoGrid>(ChildHorizontalAlignmentProperty, ChildMarginProperty,
            ChildVerticalAlignmentProperty, ColumnCountProperty, ColumnWidthProperty, IsAutoIndexingProperty, OrientationProperty,
            RowHeightProperty);
            
        ChildHorizontalAlignmentProperty.Changed.Subscribe(OnChildHorizontalAlignmentChanged);
        ChildMarginProperty.Changed.Subscribe(OnChildMarginChanged);
        ChildVerticalAlignmentProperty.Changed.Subscribe(OnChildVerticalAlignmentChanged);
        ColumnCountProperty.Changed.Subscribe(ColumnCountChanged);
        RowCountProperty.Changed.Subscribe(RowCountChanged);
        ColumnWidthProperty.Changed.Subscribe(FixedColumnWidthChanged);
        RowHeightProperty.Changed.Subscribe(FixedRowHeightChanged);
    }
    
    #region Overrides

    /// <summary>
    /// Measures the children of a <see cref="T:System.Windows.Controls.Grid"/> in anticipation of arranging them during the <see cref="M:ArrangeOverride"/> pass.
    /// </summary>
    /// <param name="constraint">Indicates an upper limit size that should not be exceeded.</param>
    /// <returns>
    /// 	<see cref="Size"/> that represents the required size to arrange child content.
    /// </returns>
    protected override Size MeasureOverride(Size constraint)
    {
        PerformLayout();
        return base.MeasureOverride(constraint);
    }
        
    #endregion Overrides
}
