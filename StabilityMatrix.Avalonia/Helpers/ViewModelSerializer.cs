using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ViewModelSerializer
{
    public static Dictionary<string, Type> GetDerivedTypes(Type baseType)
    {
        return GetJsonDerivedTypeAttributes(baseType).ToDictionary(x => x.typeDiscriminator, x => x.subType);
    }

    public static IEnumerable<(Type subType, string typeDiscriminator)> GetJsonDerivedTypeAttributes(Type type)
    {
        return type.GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(x => (x.DerivedType, x.TypeDiscriminator as string ?? x.DerivedType.Name));
    }
}
