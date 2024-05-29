using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Extensions;

public static class SkiaExtensions
{
    private record class SKBitmapDrawOperation : ICustomDrawOperation
    {
        public Rect Bounds { get; set; }

        public SKBitmap? Bitmap { get; init; }

        public void Dispose()
        {
            //nop
        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (
                Bitmap is { } bitmap
                && context.PlatformImpl.GetFeature<ISkiaSharpApiLeaseFeature>() is { } leaseFeature
            )
            {
                var lease = leaseFeature.Lease();
                using (lease)
                {
                    lease.SkCanvas.DrawBitmap(
                        bitmap,
                        SKRect.Create(
                            (float)Bounds.X,
                            (float)Bounds.Y,
                            (float)Bounds.Width,
                            (float)Bounds.Height
                        )
                    );
                }
            }
        }
    }

    private class AvaloniaImage : IImage, IDisposable
    {
        private readonly SKBitmap? _source;
        SKBitmapDrawOperation? _drawImageOperation;

        public AvaloniaImage(SKBitmap? source)
        {
            _source = source;
            if (source?.Info.Size is { } size)
            {
                Size = new Size(size.Width, size.Height);
            }
        }

        public Size Size { get; }

        public void Dispose() => _source?.Dispose();

        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
            if (_drawImageOperation is null)
            {
                _drawImageOperation = new SKBitmapDrawOperation { Bitmap = _source, };
            }

            _drawImageOperation.Bounds = sourceRect;
            context.Custom(_drawImageOperation);
        }
    }

    public static SKBitmap? ToSKBitmap(this System.IO.Stream? stream)
    {
        if (stream == null)
            return null;
        return SKBitmap.Decode(stream);
    }

    public static IImage? ToAvaloniaImage(this SKBitmap? bitmap)
    {
        if (bitmap is not null)
        {
            return new AvaloniaImage(bitmap);
        }
        return default;
    }

    public static Bitmap ToAvaloniaBitmap(this SKBitmap bitmap, Vector dpi)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var avaloniaColorFormat = bitmap.ColorType switch
        {
            SKColorType.Rgba8888 => PixelFormat.Rgba8888,
            SKColorType.Bgra8888 => PixelFormat.Bgra8888,
            _ => throw new NotSupportedException($"Unsupported SKColorType: {bitmap.ColorType}")
        };

        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var avaloniaAlphaFormat = bitmap.AlphaType switch
        {
            SKAlphaType.Opaque => AlphaFormat.Opaque,
            SKAlphaType.Premul => AlphaFormat.Premul,
            SKAlphaType.Unpremul => AlphaFormat.Unpremul,
            _ => throw new NotSupportedException($"Unsupported SKAlphaType: {bitmap.AlphaType}")
        };

        var dataPointer = bitmap.GetPixels();

        return new Bitmap(
            avaloniaColorFormat,
            avaloniaAlphaFormat,
            dataPointer,
            new PixelSize(bitmap.Width, bitmap.Height),
            dpi,
            bitmap.RowBytes
        );
    }
}
