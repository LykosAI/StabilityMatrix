using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace StabilityMatrix.Avalonia.MarkupExtensions;

/// <summary>
/// https://github.com/AvaloniaUI/Avalonia/discussions/7408
/// </summary>
/// <example>
/// <code>{e:Ternary SomeProperty, True=1, False=0}</code>
/// </example>
public class TernaryExtension : MarkupExtension
{
    public string Path { get; set; }

    public Type Type { get; set; }

    public object? True { get; set; }

    public object? False { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var cultureInfo = CultureInfo.GetCultureInfo("en-US");
        var binding = new ReflectionBindingExtension(Path)
        {
            Mode = BindingMode.OneWay,
            Converter = new FuncValueConverter<bool, object?>(
                isTrue =>
                    isTrue
                        ? Convert.ChangeType(True, Type, cultureInfo.NumberFormat)
                        : Convert.ChangeType(False, Type, cultureInfo.NumberFormat)
            )
        };

        return binding.ProvideValue(serviceProvider);
    }
}
