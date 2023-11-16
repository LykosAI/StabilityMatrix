using DynamicData;

namespace StabilityMatrix.Core.Extensions;

public static class DynamicDataExtensions
{
    /// <summary>
    /// Loads the cache with the specified items in an optimised manner i.e. calculates the differences between the old and new items
    ///  in the list and amends only the differences.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="allItems">The items to add, update or delete.</param>
    /// <exception cref="System.ArgumentNullException">source.</exception>
    public static void EditDiff<TObject, TKey>(
        this ISourceCache<TObject, TKey> source,
        IEnumerable<TObject> allItems
    )
        where TObject : IEquatable<TObject>
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (allItems is null)
        {
            throw new ArgumentNullException(nameof(allItems));
        }

        source.EditDiff(allItems, (x, y) => x.Equals(y));
    }
}
