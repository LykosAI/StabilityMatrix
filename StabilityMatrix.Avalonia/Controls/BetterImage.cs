using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace StabilityMatrix.Avalonia.Controls;

public class BetterImage : Control
{
    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<BetterImage, IImage?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<BetterImage, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<BetterImage, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    static BetterImage()
    {
        AffectsRender<BetterImage>(SourceProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<BetterImage>(SourceProperty, StretchProperty, StretchDirectionProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<BetterImage>(
            AutomationControlType.Image);
    }

    /// <summary>
    /// Gets or sets the image that will be displayed.
    /// </summary>
    [Content]
    public IImage? Source
    {
        get { return GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling how the image will be stretched.
    /// </summary>
    public Stretch Stretch
    {
        get { return GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value controlling in what direction the image will be stretched.
    /// </summary>
    public StretchDirection StretchDirection
    {
        get { return GetValue(StretchDirectionProperty); }
        set { SetValue(StretchDirectionProperty, value); }
    }

    /// <inheritdoc />
    protected override bool BypassFlowDirectionPolicies => true;

    /// <summary>
    /// Renders the control.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    public sealed override void Render(DrawingContext context)
    {
        var source = Source;

        if (source == null || Bounds is not {Width: > 0, Height: > 0}) return;
        
        var viewPort = new Rect(Bounds.Size);
        var sourceSize = source.Size;

        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
            
        // Calculate starting points for dest
        var destX = HorizontalAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Center => (int) (viewPort.Width - scaledSize.Width) / 2,
            HorizontalAlignment.Right => (int) (viewPort.Width - scaledSize.Width),
            // Stretch is default, use center
            HorizontalAlignment.Stretch => (int) (viewPort.Width - scaledSize.Width) / 2, 
            _ => throw new ArgumentException(nameof(HorizontalAlignment))
        };
            
        var destRect = viewPort
            .CenterRect(new Rect(scaledSize))
            .WithX(destX)
            .WithY(0)
            .Intersect(viewPort);
        var destRectUnscaledSize = destRect.Size / scale;
            
        var sourceX = HorizontalAlignment switch
        {
            HorizontalAlignment.Left => 0,
            HorizontalAlignment.Center => (int) (sourceSize - destRectUnscaledSize).Width / 2,
            HorizontalAlignment.Right => (int) (sourceSize - destRectUnscaledSize).Width,
            // Stretch is default, use center
            HorizontalAlignment.Stretch => (int) (sourceSize - destRectUnscaledSize).Width / 2, 
            _ => throw new ArgumentException(nameof(HorizontalAlignment))
        };
            
        var sourceRect = new Rect(sourceSize)
            .CenterRect(new Rect(destRect.Size / scale))
            .WithX(sourceX)
            .WithY(0);

        context.DrawImage(source, sourceRect, destRect);
    }

    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        var source = Source;
        var result = new Size();

        if (source != null)
        {
            result = Stretch.CalculateSize(availableSize, source.Size, StretchDirection);
        }

        return result;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var source = Source;

        if (source != null)
        {
            var sourceSize = source.Size;
            var result = Stretch.CalculateSize(finalSize, sourceSize);
            return result;
        }
        else
        {
            return new Size();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ImageAutomationPeer(this);
    }
}
