namespace StabilityMatrix.Core.Extensions;

public static class UriExtensions
{
    /// <summary>
    /// Return a new <see cref="Uri"/> with the given paths appended to the original.
    /// </summary>
    public static Uri Append(this Uri uri, params string[] paths)
    {
        return new Uri(
            paths.Aggregate(
                uri.AbsoluteUri,
                (current, path) => $"{current.TrimEnd('/')}/{path.TrimStart('/')}"
            )
        );
    }
}
