using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;

namespace StabilityMatrix.Avalonia.Controls.TextMarkers;

public sealed class TextMarker : TextSegment
{
    private readonly TextMarkerService _service;

    public TextMarker(TextMarkerService service, int startOffset, int length)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        StartOffset = startOffset;
        Length = length;
    }

    public event EventHandler? Deleted;

    public bool IsDeleted => !IsConnectedToCollection;

    public void Delete()
    {
        _service.Remove(this);
    }

    internal void OnDeleted()
    {
        Deleted?.Invoke(this, EventArgs.Empty);
    }

    private void Redraw()
    {
        _service.Redraw(this);
    }

    private Color? _backgroundColor;

    public Color? BackgroundColor
    {
        get => _backgroundColor; set
        {
            if (!EqualityComparer<Color?>.Default.Equals(_backgroundColor, value))
            {
                _backgroundColor = value;
                Redraw();
            }
        }
    }

    private Color? _foregroundColor;

    public Color? ForegroundColor
    {
        get => _foregroundColor; set
        {
            if (!EqualityComparer<Color?>.Default.Equals(_foregroundColor, value))
            {
                _foregroundColor = value;
                Redraw();
            }
        }
    }

    private FontWeight? _fontWeight;

    public FontWeight? FontWeight
    {
        get => _fontWeight; set
        {
            if (_fontWeight != value)
            {
                _fontWeight = value;
                Redraw();
            }
        }
    }

    private FontStyle? _fontStyle;

    public FontStyle? FontStyle
    {
        get => _fontStyle; set
        {
            if (_fontStyle != value)
            {
                _fontStyle = value;
                Redraw();
            }
        }
    }

    public object? Tag { get; set; }

    private Color _markerColor;

    public Color MarkerColor
    {
        get => _markerColor; set
        {
            if (!EqualityComparer<Color>.Default.Equals(_markerColor, value))
            {
                _markerColor = value;
                Redraw();
            }
        }
    }

    public object? ToolTip { get; set; }
}
