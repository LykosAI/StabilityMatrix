using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Extensions;

[PublicAPI]
public static class JsonObjectExtensions
{
    /// <summary>
    /// Returns the value of a property with the specified name, or the specified default value if not found.
    /// </summary>
    public static T? GetPropertyValueOrDefault<T>(
        this JsonObject jsonObject,
        string propertyName,
        T? defaultValue = default
    )
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node))
        {
            return defaultValue;
        }

        return node.Deserialize<T>();
    }

    /// <summary>
    /// Get a keyed value from a JsonObject if it is not null,
    /// otherwise add and return a new instance of a JsonObject.
    /// </summary>
    public static JsonObject GetOrAddNonNullJsonObject(this JsonObject jsonObject, string key)
    {
        if (jsonObject.TryGetPropertyValue(key, out var value) && value is JsonObject jsonObjectValue)
        {
            return jsonObjectValue;
        }

        var newJsonObject = new JsonObject();
        jsonObject[key] = newJsonObject;
        return newJsonObject;
    }

    /// <summary>
    /// Get a keyed value path from a JsonObject if it is not null,
    /// otherwise add and return a new instance of a JsonObject.
    /// </summary>
    public static JsonObject GetOrAddNonNullJsonObject(
        this JsonObject jsonObject,
        IEnumerable<string> keyPath
    )
    {
        return keyPath.Aggregate(jsonObject, (current, key) => current.GetOrAddNonNullJsonObject(key));
    }
}
