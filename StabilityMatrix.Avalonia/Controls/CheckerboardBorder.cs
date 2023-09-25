using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class CheckerboardBorder : Control
{
    public static readonly StyledProperty<byte> GridCellSizeProperty = AvaloniaProperty.Register<
        AdvancedImageBox,
        byte
    >(nameof(GridCellSize), 15);

    public byte GridCellSize
    {
        get => GetValue(GridCellSizeProperty);
        set => SetValue(GridCellSizeProperty, value);
    }

    public static readonly StyledProperty<ISolidColorBrush> GridColorProperty =
        AvaloniaProperty.Register<AdvancedImageBox, ISolidColorBrush>(
            nameof(GridColor),
            SolidColorBrush.Parse("#181818")
        );

    /// <summary>
    /// Gets or sets the color used to create the checkerboard style background
    /// </summary>
    public ISolidColorBrush GridColor
    {
        get => GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public static readonly StyledProperty<ISolidColorBrush> GridColorAlternateProperty =
        AvaloniaProperty.Register<AdvancedImageBox, ISolidColorBrush>(
            nameof(GridColorAlternate),
            SolidColorBrush.Parse("#252525")
        );

    /// <summary>
    /// Gets or sets the color used to create the checkerboard style background
    /// </summary>
    public ISolidColorBrush GridColorAlternate
    {
        get => GetValue(GridColorAlternateProperty);
        set => SetValue(GridColorAlternateProperty, value);
    }

    static CheckerboardBorder()
    {
        AffectsRender<CheckerboardBorder>(GridCellSizeProperty);
        AffectsRender<CheckerboardBorder>(GridColorProperty);
        AffectsRender<CheckerboardBorder>(GridColorAlternateProperty);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var size = GridCellSize;

        var square1Drawing = new GeometryDrawing
        {
            Brush = GridColorAlternate,
            Geometry = new RectangleGeometry(new Rect(0.0, 0.0, size, size))
        };

        var square2Drawing = new GeometryDrawing
        {
            Brush = GridColorAlternate,
            Geometry = new RectangleGeometry(new Rect(size, size, size, size))
        };

        var drawingGroup = new DrawingGroup { Children = { square1Drawing, square2Drawing } };

        var tileBrush = new DrawingBrush(drawingGroup)
        {
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            DestinationRect = new RelativeRect(new Size(2 * size, 2 * size), RelativeUnit.Absolute),
            Stretch = Stretch.None,
            TileMode = TileMode.Tile,
        };

        context.FillRectangle(GridColor, Bounds);
        // context.DrawRectangle(new Pen(Brushes.Blue), new Rect(0.5, 0.5, Bounds.Width - 1.0, Bounds.Height - 1.0));

        context.FillRectangle(tileBrush, Bounds);

        // base.Render(context);
    }
}
