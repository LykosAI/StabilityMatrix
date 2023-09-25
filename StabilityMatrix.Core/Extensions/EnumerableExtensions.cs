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
    
    /// <summary>
    /// Nested for loop helper
    /// </summary>
    public static IEnumerable<(T, T)> Product<T>(this IEnumerable<T> items, IEnumerable<T> other)
    {
        return from item1 in items
            from item2 in other
            select (item1, item2);
    } 
    
    public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(
        this IEnumerable<TSource> source, Func<TSource, Task<TResult>> method,
        int concurrency = int.MaxValue)
    {
        using var semaphore = new SemaphoreSlim(concurrency);
        return await Task.WhenAll(source.Select(async s =>
        {
            try
            {
                // ReSharper disable once AccessToDisposedClosure
                await semaphore.WaitAsync().ConfigureAwait(false);
                return await method(s).ConfigureAwait(false);
            }
            finally
            {
                // ReSharper disable once AccessToDisposedClosure
                semaphore.Release();
            }
        })).ConfigureAwait(false);
    }
}
