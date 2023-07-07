namespace StabilityMatrix.Core.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<(int, T)> Enumerate<T>(
        this IEnumerable<T> items,
        int start
    ) {
        return items.Select((item, index) => (index + start, item));
    }
    
    public static IEnumerable<(int, T)> Enumerate<T>(
        this IEnumerable<T> items
    ) {
        return items.Select((item, index) => (index, item));
    }
}
