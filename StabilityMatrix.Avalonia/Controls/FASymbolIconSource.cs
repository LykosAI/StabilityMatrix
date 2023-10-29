using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using Projektanker.Icons.Avalonia;

namespace StabilityMatrix.Avalonia.Controls;

[TypeConverter(typeof(FASymbolIconSourceConverter))]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class FASymbolIconSource : PathIconSource
{
    public static readonly StyledProperty<string> SymbolProperty = AvaloniaProperty.Register<
        FASymbolIconSource,
        string
    >(nameof(Symbol));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<FASymbolIconSource>();

    public FASymbolIconSource()
    {
        Stretch = Stretch.None;
        // FontSize = 20; // Override value inherited from visual parents.
        InvalidateData();
    }

    public string Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SymbolProperty || change.Property == FontSizeProperty)
        {
            InvalidateData();
        }
    }

    private void InvalidateData()
    {
        var path = IconProvider.Current.GetIcon(Symbol).Path;
        var geometry = Geometry.Parse(path);

        var scale = FontSize / 20;

        Data = geometry;
        // TODO: Scaling not working
        Data.Transform = new ScaleTransform(scale, scale);
    }
}

public class FASymbolIconSourceConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        if (sourceType == typeof(string))
        {
            return true;
        }
        return base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value
    )
    {
        return value switch
        {
            string val => new FASymbolIconSource { Symbol = val, },
            _ => base.ConvertFrom(context, culture, value)
        };
    }
}
