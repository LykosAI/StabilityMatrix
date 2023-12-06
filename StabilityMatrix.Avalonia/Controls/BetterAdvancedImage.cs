using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public class BetterAdvancedImage : AdvancedImage
{
    #region Reflection Shenanigans to access private parent fields
    [NotNull]
    private static readonly FieldInfo? IsCornerRadiusUsedField = typeof(AdvancedImage).GetField(
        "_isCornerRadiusUsed",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    [NotNull]
    private static readonly FieldInfo? CornerRadiusClipField = typeof(AdvancedImage).GetField(
        "_cornerRadiusClip",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private bool IsCornerRadiusUsed
    {
        get => IsCornerRadiusUsedField.GetValue(this) as bool? ?? false;
        set => IsCornerRadiusUsedField.SetValue(this, value);
    }

    private RoundedRect CornerRadiusClip
    {
        get => (RoundedRect)CornerRadiusClipField.GetValue(this)!;
        set => CornerRadiusClipField.SetValue(this, value);
    }

    static BetterAdvancedImage()
    {
        if (IsCornerRadiusUsedField is null)
        {
            throw new NullReferenceException("IsCornerRadiusUsedField was not resolved");
        }
        if (CornerRadiusClipField is null)
        {
            throw new NullReferenceException("CornerRadiusClipField was not resolved");
        }
    }
    #endregion

    protected override Type StyleKeyOverride { get; } = typeof(AdvancedImage);

    public BetterAdvancedImage(Uri? baseUri)
        : base(baseUri) { }

    public BetterAdvancedImage(IServiceProvider serviceProvider)
        : base(serviceProvider) { }

    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var source = CurrentImage;

        if (source != null && Bounds is { Width: > 0, Height: > 0 })
        {
            var viewPort = new Rect(Bounds.Size);
            var sourceSize = source.Size;

            var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
            var scaledSize = sourceSize * scale;

            // Calculate starting points for dest
            var destX = HorizontalContentAlignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Center => (int)(viewPort.Width - scaledSize.Width) / 2,
                HorizontalAlignment.Right => (int)(viewPort.Width - scaledSize.Width),
                // Stretch is default, use center
                HorizontalAlignment.Stretch
                    => (int)(viewPort.Width - scaledSize.Width) / 2,
                _ => throw new ArgumentException(nameof(HorizontalContentAlignment))
            };
            var destY = VerticalContentAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Center => (int)(viewPort.Height - scaledSize.Height) / 2,
                VerticalAlignment.Bottom => (int)(viewPort.Height - scaledSize.Height),
                VerticalAlignment.Stretch => 0, // Stretch is default, use top
                _ => throw new ArgumentException(nameof(VerticalContentAlignment))
            };

            var destRect = viewPort.CenterRect(new Rect(scaledSize)).WithX(destX).WithY(destY).Intersect(viewPort);
            var destRectUnscaledSize = destRect.Size / scale;

            // Calculate starting points for source
            var sourceX = HorizontalContentAlignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Center => (int)(sourceSize - destRectUnscaledSize).Width / 2,
                HorizontalAlignment.Right => (int)(sourceSize - destRectUnscaledSize).Width,
                // Stretch is default, use center
                HorizontalAlignment.Stretch
                    => (int)(sourceSize - destRectUnscaledSize).Width / 2,
                _ => throw new ArgumentException(nameof(HorizontalContentAlignment))
            };
            var sourceY = VerticalContentAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Center => (int)(sourceSize - destRectUnscaledSize).Height / 2,
                VerticalAlignment.Bottom => (int)(sourceSize - destRectUnscaledSize).Height,
                VerticalAlignment.Stretch => 0, // Stretch is default, use top
                _ => throw new ArgumentException(nameof(VerticalContentAlignment))
            };

            var sourceRect = new Rect(sourceSize)
                .CenterRect(new Rect(destRect.Size / scale))
                .WithX(sourceX)
                .WithY(sourceY);

            if (IsCornerRadiusUsed)
            {
                using (context.PushClip(CornerRadiusClip))
                {
                    context.DrawImage(source, sourceRect, destRect);
                }
            }
            else
            {
                context.DrawImage(source, sourceRect, destRect);
            }
        }
        else
        {
            base.Render(context);
        }
    }
}
