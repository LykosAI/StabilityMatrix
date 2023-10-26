using System.Text.Json;
using System.Text.Json.Nodes;

namespace StabilityMatrix.Core.Extensions;

public static class JsonObjectExtensions
{
    /// <summary>
    /// Returns the value of a property with the specified name, or the specified default value if not found.
    /// </summary>
    public static T GetPropertyValueOrDefault<T>(
        this JsonObject jsonObject,
        string propertyName,
        T defaultValue
    )
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node))
        {
            return defaultValue;
        }

        return node.Deserialize<T>() ?? defaultValue;
    }
}
