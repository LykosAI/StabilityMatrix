using System.Text;

namespace StabilityMatrix.Core.Extensions;

public static class DictionaryExtensions
{
    /// <summary>
    /// Adds all items from another dictionary to this dictionary.
    /// </summary>
    public static void Update<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        IReadOnlyDictionary<TKey, TValue> collection
    )
        where TKey : notnull
    {
        foreach (var item in collection)
        {
            source[item.Key] = item.Value;
        }
    }
    
    /// <summary>
    /// Formats a dictionary as a string for debug/logging purposes.
    /// </summary>
    public static string ToRepr<TKey, TValue>(
        this IDictionary<TKey, TValue> source
    )
        where TKey : notnull
    {
        var sb = new StringBuilder();
        sb.Append('{');
        foreach (var (key, value) in source)
        {
            // for string types, use ToRepr
            if (key is string keyString)
            {
                sb.Append($"{keyString.ToRepr()}=");
            }
            else
            {
                sb.Append($"{key}=");
            }
            
            if (value is string valueString)
            {
                sb.Append($"{valueString.ToRepr()}, ");
            }
            else
            {
                sb.Append($"{value}, ");
            }
        }
        sb.Append('}');
        return sb.ToString();
    }
}
