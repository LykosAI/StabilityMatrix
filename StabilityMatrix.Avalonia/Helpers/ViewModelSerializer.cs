using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ViewModelSerializer
{
    public static IImmutableDictionary<string, Type> GetDerivedTypes(Type baseType)
    {
        return GetJsonDerivedTypeAttributes(baseType)
            .ToImmutableDictionary(x => x.typeDiscriminator, x => x.subType);
    }

    public static IEnumerable<(
        Type subType,
        string typeDiscriminator
    )> GetJsonDerivedTypeAttributes(Type type)
    {
        return type.GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(x => (x.DerivedType, x.TypeDiscriminator as string ?? x.DerivedType.Name));
    }
}
