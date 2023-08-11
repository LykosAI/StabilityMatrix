using System.Reflection;
using RockLib.Reflection.Optimized;

namespace StabilityMatrix.Core.Extensions;


public static class ObjectExtensions
{
    /// <summary>
    /// Cache of Types to named field getters
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<string, Func<object, object>>> FieldGetterTypeCache = new();
    
    /// <summary>
    /// Get the value of a named private field from an object
    /// </summary>
    public static T? GetPrivateField<T>(this object obj, string fieldName)
    {
        // Check cache
        var fieldGetterCache = FieldGetterTypeCache.GetOrAdd(obj.GetType());

        if (!fieldGetterCache.TryGetValue(fieldName, out var fieldGetter))
        {
            // Get the field
            var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is null)
            {
                throw new ArgumentException($"Field {fieldName} not found on type {obj.GetType().Name}");
            }
            
            // Create a getter for the field
            fieldGetter = field.CreateGetter();
            
            // Add to cache
            fieldGetterCache.Add(fieldName, fieldGetter);
        }

        return (T?) fieldGetter(obj);
    }
}
