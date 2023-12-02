using System.Reflection;
using JetBrains.Annotations;
using RockLib.Reflection.Optimized;

namespace StabilityMatrix.Core.Extensions;

[PublicAPI]
public static class ObjectExtensions
{
    /// <summary>
    /// Cache of Types to named field getters
    /// </summary>
    private static readonly Dictionary<
        Type,
        Dictionary<string, Func<object, object>>
    > FieldGetterTypeCache = new();

    /// <summary>
    /// Cache of Types to named field setters
    /// </summary>
    private static readonly Dictionary<
        Type,
        Dictionary<string, Action<object, object>>
    > FieldSetterTypeCache = new();

    /// <summary>
    /// Get the value of a named private field from an object
    /// </summary>
    /// <remarks>
    /// The field must be defined by the runtime type of <see cref="obj"/> or its first base type.
    /// For higher inheritance levels, use <see cref="GetPrivateField{TObject,T}"/> to specify the exact defining type.
    /// </remarks>
    public static T? GetPrivateField<T>(this object obj, string fieldName)
    {
        // Check cache
        var fieldGetterCache = FieldGetterTypeCache.GetOrAdd(obj.GetType());

        if (!fieldGetterCache.TryGetValue(fieldName, out var fieldGetter))
        {
            // Get the field
            var field = obj.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            // Try get from parent
            field ??= obj.GetType()
                .BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field is null)
            {
                throw new ArgumentException(
                    $"Field {fieldName} not found on type {obj.GetType().Name}"
                );
            }

            // Create a getter for the field
            fieldGetter = field.CreateGetter();

            // Add to cache
            fieldGetterCache.Add(fieldName, fieldGetter);
        }

        return (T?)fieldGetter(obj);
    }

    /// <summary>
    /// Get the value of a named private field from an object
    /// </summary>
    /// <typeparam name="TObject">Type of the object that defines the field, must be a base class of <see cref="obj"/></typeparam>
    /// <typeparam name="T">Type of the field</typeparam>
    public static T? GetPrivateField<TObject, T>(this TObject obj, string fieldName)
        where TObject : class
    {
        // Check cache
        var fieldGetterCache = FieldGetterTypeCache.GetOrAdd(typeof(TObject));

        if (!fieldGetterCache.TryGetValue(fieldName, out var fieldGetter))
        {
            // Get the field
            var field = typeof(TObject).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (field is null)
            {
                throw new ArgumentException(
                    $"Field {typeof(TObject).Name}.{fieldName} not found on type {obj.GetType().Name}"
                );
            }

            // Create a getter for the field
            fieldGetter = field.CreateGetter();

            // Add to cache
            fieldGetterCache.Add(fieldName, fieldGetter);
        }

        return (T?)fieldGetter(obj);
    }

    /// <summary>
    /// Set the value of a named private field on an object
    /// </summary>
    public static void SetPrivateField(this object obj, string fieldName, object value)
    {
        // Check cache
        var fieldSetterCache = FieldSetterTypeCache.GetOrAdd(obj.GetType());

        if (!fieldSetterCache.TryGetValue(fieldName, out var fieldSetter))
        {
            // Get the field
            var field = obj.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            // Try get from parent
            field ??= obj.GetType()
                .BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field is null)
            {
                throw new ArgumentException(
                    $"Field {fieldName} not found on type {obj.GetType().Name}"
                );
            }

            // Create a setter for the field
            fieldSetter = field.CreateSetter();

            // Add to cache
            fieldSetterCache.Add(fieldName, fieldSetter);
        }

        fieldSetter(obj, value);
    }

    /// <summary>
    /// Set the value of a named private field on an object
    /// </summary>
    public static void SetPrivateField<T>(this object obj, string fieldName, T? value)
    {
        // Check cache
        var fieldSetterCache = FieldSetterTypeCache.GetOrAdd(obj.GetType());

        if (!fieldSetterCache.TryGetValue(fieldName, out var fieldSetter))
        {
            // Get the field
            var field = obj.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            // Try get from parent
            field ??= obj.GetType()
                .BaseType?.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field is null)
            {
                throw new ArgumentException(
                    $"Field {fieldName} not found on type {obj.GetType().Name}"
                );
            }

            // Create a setter for the field
            fieldSetter = field.CreateSetter();

            // Add to cache
            fieldSetterCache.Add(fieldName, fieldSetter);
        }

        fieldSetter(obj, value!);
    }
}
