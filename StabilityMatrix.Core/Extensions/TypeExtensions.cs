using System.Reflection;

namespace StabilityMatrix.Core.Extensions;

public static class TypeExtensions
{
    /// <summary>
    /// Get all properties marked with an attribute of type <see cref="TAttribute"/>
    /// </summary>
    public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<TAttribute>(this Type type)
        where TAttribute : Attribute
    {
        return type.GetProperties().Where(p => Attribute.IsDefined(p, typeof(TAttribute)));
    }
}
