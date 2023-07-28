namespace StabilityMatrix.Core.Extensions;

public static class DictionaryExtensions
{
    /// <summary>
    /// Adds all items from another dictionary to this dictionary.
    /// </summary>
    public static void Update<TKey, TValue>(
        this Dictionary<TKey, TValue> source,
        IReadOnlyDictionary<TKey, TValue> collection
    )
        where TKey : notnull
    {
        foreach (var item in collection)
        {
            source[item.Key] = item.Value;
        }
    }
}
