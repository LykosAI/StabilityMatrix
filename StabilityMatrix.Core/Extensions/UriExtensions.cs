using System.Web;

namespace StabilityMatrix.Core.Extensions;

public static class UriExtensions
{
    public static Uri WithQuery(this Uri uri, string key, string value)
    {
        var builder = new UriBuilder(uri);
        var query = HttpUtility.ParseQueryString(builder.Query);
        query[key] = value;
        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri;
    }

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
