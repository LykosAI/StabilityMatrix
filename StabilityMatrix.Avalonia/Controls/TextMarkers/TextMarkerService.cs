// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CommonBrush = Avalonia.Media.IBrush;

namespace StabilityMatrix.Avalonia.Controls.TextMarkers;

public sealed class TextMarkerService : DocumentColorizingTransformer, IBackgroundRenderer, ITextViewConnect
{
    private readonly TextSegmentCollection<TextMarker> _markers;
    private readonly TextDocument _document;
    private readonly List<TextView> _textViews;

    public TextMarkerService(TextEditor editor)
    {
        if (editor == null) throw new ArgumentNullException(nameof(editor));
        _document = editor.Document;
        _markers = new TextSegmentCollection<TextMarker>(_document);
        _textViews = new List<TextView>();
        // editor.ToolTipRequest += EditorOnToolTipRequest;
    }

    /*private void EditorOnToolTipRequest(object? sender, ToolTipRequestEventArgs args)
    {
        var offset = _document.GetOffset(args.LogicalPosition);

        //FoldingManager foldings = _editor.GetService(typeof(FoldingManager)) as FoldingManager;
        //if (foldings != null)
        //{
        //    var foldingsAtOffset = foldings.GetFoldingsAt(offset);
        //    FoldingSection collapsedSection = foldingsAtOffset.FirstOrDefault(section => section.IsFolded);

        //    if (collapsedSection != null)
        //    {
        //        args.SetToolTip(GetTooltipTextForCollapsedSection(args, collapsedSection));
        //    }
        //}

        var markersAtOffset = GetMarkersAtOffset(offset);
        var markerWithToolTip = markersAtOffset.FirstOrDefault(marker => marker.ToolTip != null);
        if (markerWithToolTip != null && markerWithToolTip.ToolTip != null)
        {
            args.SetToolTip(markerWithToolTip.ToolTip);
        }
    }*/

    #region TextMarkerService

    public TextMarker? TryCreate(int startOffset, int length)
    {
        if (_markers == null)
            throw new InvalidOperationException("Cannot create a marker when not attached to a document");

        var textLength = _document.TextLength;
        if (startOffset < 0 || startOffset > textLength) return null;
        //throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Value must be between 0 and " + textLength);
        if (length < 0 || startOffset + length > textLength) return null;
        //throw new ArgumentOutOfRangeException(nameof(length), length, "length must not be negative and startOffset+length must not be after the end of the document");

        var marker = new TextMarker(this, startOffset, length);
        _markers.Add(marker);
        return marker;
    }

    public IEnumerable<TextMarker> GetMarkersAtOffset(int offset)
    {
        return _markers.FindSegmentsContaining(offset);
    }

    public IEnumerable<TextMarker> TextMarkers => _markers ?? Enumerable.Empty<TextMarker>();

    public void RemoveAll(Predicate<TextMarker> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        
        foreach (var m in _markers.ToArray())
        {
            if (predicate(m))
                Remove(m);
        }
    }

    public void Remove(TextMarker? marker)
    {
        if (marker == null) throw new ArgumentNullException(nameof(marker));

        if (_markers.Remove(marker))
        {
            Redraw(marker);
            marker.OnDeleted();
        }
    }

    internal void Redraw(ISegment segment)
    {
        foreach (var view in _textViews)
        {
            view.Redraw(segment);
        }
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RedrawRequested;

    #endregion

    #region DocumentColorizingTransformer

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineStart = line.Offset;
        var lineEnd = lineStart + line.Length;
        foreach (var marker in _markers.FindOverlappingSegments(lineStart, line.Length))
        {
            CommonBrush? foregroundBrush = null;
            if (marker.ForegroundColor != null)
            {
                foregroundBrush = new SolidColorBrush(marker.ForegroundColor.Value).ToImmutable();
            }
            ChangeLinePart(
                Math.Max(marker.StartOffset, lineStart),
                Math.Min(marker.EndOffset, lineEnd),
                element =>
                {
                    if (foregroundBrush != null)
                    {
                        element.TextRunProperties.SetForegroundBrush(foregroundBrush);
                    }
                    var tf = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(
                        tf.FontFamily,
                        marker.FontStyle ?? tf.Style,
                        marker.FontWeight ?? tf.Weight,
                        tf.Stretch
                    ));
                }
            );
        }
    }

    #endregion

    #region IBackgroundRenderer

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView == null)
            throw new ArgumentNullException(nameof(textView));
        if (drawingContext == null)
            throw new ArgumentNullException(nameof(drawingContext));
        if (!textView.VisualLinesValid)
            return;
        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0)
            return;
        var viewStart = visualLines.First().FirstDocumentLine.Offset;
        var viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
        foreach (var marker in _markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
        {
            if (marker.BackgroundColor != null)
            {
                var geoBuilder = new BackgroundGeometryBuilder
                {
                    AlignToWholePixels = true,
                    CornerRadius = 3
                };
                geoBuilder.AddSegment(textView, marker);
                var geometry = geoBuilder.CreateGeometry();
                if (geometry != null)
                {
                    var color = marker.BackgroundColor.Value;
                    var brush = new SolidColorBrush(color).ToImmutable();
                    drawingContext.DrawGeometry(brush, null, geometry);
                }
            }
            foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
            {
                var startPoint = r.BottomLeft;
                var endPoint = r.BottomRight;

                var usedBrush = new SolidColorBrush(marker.MarkerColor).ToImmutable();
                var offset = 2.5;

                var count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);

                /*var geometry = new StreamGeometry();

                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(startPoint, false);
                    // ctx.PolyLineTo(CreatePoints(startPoint, offset, count).ToArray(), true, false);
                    ctx.LineTo(CreatePoints(startPoint, offset, count).ToArray());
                }*/

                var geometry = new PolylineGeometry(CreatePoints(startPoint, offset, count), false);

                // geometry.Freeze();

                var usedPen = new Pen(usedBrush, 1);
                // usedPen.Freeze();
                drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
            }
        }
    }

    private static IEnumerable<Point> CreatePoints(Point start, double offset, int count)
    {
        for (var i = 0; i < count; i++)
            yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
    }

    #endregion

    #region ITextViewConnect

    void ITextViewConnect.AddToTextView(TextView textView)
    {
        if (textView != null && !_textViews.Contains(textView))
        {
            Debug.Assert(textView.Document == _document);
            _textViews.Add(textView);
        }
    }

    void ITextViewConnect.RemoveFromTextView(TextView textView)
    {
        if (textView != null)
        {
            Debug.Assert(textView.Document == _document);
            _textViews.Remove(textView);
        }
    }

    #endregion
}
