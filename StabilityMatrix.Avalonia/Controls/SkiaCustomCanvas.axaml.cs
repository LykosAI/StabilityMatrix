using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace StabilityMatrix.Avalonia.Controls;

public partial class SkiaCustomCanvas : UserControl
{
    private readonly RenderingLogic renderingLogic = new();

    public event Action<SKSurface>? RenderSkia;

    public SkiaCustomCanvas()
    {
        InitializeComponent();

        Background = Brushes.Transparent;

        renderingLogic.RenderCall += surface => RenderSkia?.Invoke(surface);
    }

    public override void Render(DrawingContext context)
    {
        renderingLogic.Bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        context.Custom(renderingLogic);
    }

    private class RenderingLogic : ICustomDrawOperation
    {
        public Action<SKSurface>? RenderCall;

        public Rect Bounds { get; set; }

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other == this;
        }

        /// <inheritdoc />
        public bool HitTest(Point p)
        {
            return false;
        }

        /// <inheritdoc />
        public void Render(ImmediateDrawingContext context)
        {
            var skia = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();

            using var lease = skia?.Lease();

            if (lease?.SkSurface is { } skSurface)
            {
                Render(skSurface);
            }
        }

        private void Render(SKSurface surface)
        {
            RenderCall?.Invoke(surface);
        }
    }
}
