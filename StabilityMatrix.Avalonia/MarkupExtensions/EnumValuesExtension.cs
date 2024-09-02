using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Markup.Xaml;

namespace StabilityMatrix.Avalonia.MarkupExtensions;

public class EnumValuesExtension<TEnum> : MarkupExtension
    where TEnum : struct, Enum
{
    public TEnum? MinValue { get; set; }

    public TEnum? MaxValue { get; set; }

    public bool NoDefault { get; set; }

    public override IEnumerable<TEnum> ProvideValue(IServiceProvider serviceProvider)
    {
        var values = Enum.GetValues<TEnum>().AsEnumerable();

        if (NoDefault)
        {
            values = values.Where(value => !EqualityComparer<TEnum>.Default.Equals(value, default));
        }

        if (MinValue is not null)
        {
            values = values.Where(value => Comparer<TEnum>.Default.Compare(value, MinValue.Value) >= 0);
        }

        if (MaxValue is not null)
        {
            values = values.Where(value => Comparer<TEnum>.Default.Compare(value, MaxValue.Value) <= 0);
        }

        return values;
    }
}

public class EnumValuesExtension : MarkupExtension
{
    public Type? Type { get; set; }

    public int? MinValue { get; set; }

    public int? MaxValue { get; set; }

    public bool NoDefault { get; set; }

    public override IEnumerable<Enum> ProvideValue(IServiceProvider serviceProvider)
    {
        if (Type is null)
        {
            return [];
        }

        var values = Enum.GetValues(Type).Cast<Enum>().AsEnumerable();

        if (NoDefault)
        {
            values = values.Where(value => value.CompareTo(default) != 0);
        }

        if (MinValue is not null)
        {
            values = values.Where(value => value.CompareTo(MinValue.Value) >= 0);
        }

        if (MaxValue is not null)
        {
            values = values.Where(value => value.CompareTo(MaxValue.Value) <= 0);
        }

        return values;
    }
}
