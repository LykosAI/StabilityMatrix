using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using StabilityMatrix.Avalonia.Converters.Json;
using JsonException = System.Text.Json.JsonException;

namespace StabilityMatrix.Avalonia.Helpers;

public static class ViewModelSerializer
{
    public static JsonSerializerSettings SerializeSettings { get; } =
        new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Arrays,
            NullValueHandling = NullValueHandling.Ignore
        };

    public static JsonSerializerSettings DeserializeSettings { get; } =
        new() { TypeNameHandling = TypeNameHandling.Arrays };

    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        SerializeSettings.ContractResolver = new ServiceProviderContractResolver(serviceProvider);
        DeserializeSettings.ContractResolver = new ServiceProviderContractResolver(serviceProvider);
    }

    public static JsonObject SerializeToJsonObject<T>(T target)
    {
        var result = JsonConvert.SerializeObject(target, SerializeSettings);
        return JsonNode.Parse(result)?.AsObject() ?? throw new JsonException();
    }

    public static T? DeserializeJsonObject<T>(JsonObject jsonObject)
    {
        var result = JsonConvert.DeserializeObject<T>(
            jsonObject.ToJsonString(),
            DeserializeSettings
        );
        return result;
    }

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
