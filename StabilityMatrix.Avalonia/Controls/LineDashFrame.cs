using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace StabilityMatrix.Avalonia.Controls;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class LineDashFrame : Frame
{
    protected override Type StyleKeyOverride { get; } = typeof(Frame);

    public static readonly StyledProperty<ISolidColorBrush> StrokeProperty =
        AvaloniaProperty.Register<LineDashFrame, ISolidColorBrush>("Stroke");

    public ISolidColorBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<LineDashFrame, double>("StrokeThickness");

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly StyledProperty<double> StrokeDashLineProperty =
        AvaloniaProperty.Register<LineDashFrame, double>("StrokeDashLine");

    public double StrokeDashLine
    {
        get => GetValue(StrokeDashLineProperty);
        set => SetValue(StrokeDashLineProperty, value);
    }

    public static readonly StyledProperty<double> StrokeDashSpaceProperty =
        AvaloniaProperty.Register<LineDashFrame, double>("StrokeDashSpace");

    public double StrokeDashSpace
    {
        get => GetValue(StrokeDashSpaceProperty);
        set => SetValue(StrokeDashSpaceProperty, value);
    }

    public static readonly StyledProperty<ISolidColorBrush> FillProperty =
        AvaloniaProperty.Register<LineDashFrame, ISolidColorBrush>("Fill");

    public ISolidColorBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public LineDashFrame()
    {
        UseLayoutRounding = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (
            change.Property == StrokeProperty
            || change.Property == StrokeThicknessProperty
            || change.Property == StrokeDashLineProperty
            || change.Property == StrokeDashSpaceProperty
            || change.Property == FillProperty
        )
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;

        context.DrawRectangle(Fill, null, new Rect(0, 0, width, height));

        var dashPen = new Pen(Stroke, StrokeThickness)
        {
            DashStyle = new DashStyle(GetDashArray(width), 0)
        };

        context.DrawLine(dashPen, new Point(0, 0), new Point(width, 0));
        context.DrawLine(dashPen, new Point(0, height), new Point(width, height));
        context.DrawLine(dashPen, new Point(0, 0), new Point(0, height));
        context.DrawLine(dashPen, new Point(width, 0), new Point(width, height));
    }

    private IEnumerable<double> GetDashArray(double length)
    {
        var availableLength = length - StrokeDashLine;
        var lines = (int)Math.Round(availableLength / (StrokeDashLine + StrokeDashSpace));
        availableLength -= lines * StrokeDashLine;
        var actualSpacing = availableLength / lines;

        yield return StrokeDashLine / StrokeThickness;
        yield return actualSpacing / StrokeThickness;
    }
}
