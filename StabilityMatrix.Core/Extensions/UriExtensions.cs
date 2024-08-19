using System.ComponentModel;
using System.Web;

namespace StabilityMatrix.Core.Extensions;

[Localizable(false)]
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

    /// <summary>
    /// Returns a new Uri with the query values redacted.
    /// Non-empty query values are replaced with a single asterisk.
    /// </summary>
    public static Uri RedactQueryValues(this Uri uri)
    {
        var builder = new UriBuilder(uri);

        var queryCollection = HttpUtility.ParseQueryString(builder.Query);

        foreach (var key in queryCollection.AllKeys)
        {
            if (!string.IsNullOrEmpty(queryCollection[key]))
            {
                queryCollection[key] = "*";
            }
        }

        builder.Query = queryCollection.ToString() ?? string.Empty;
        return builder.Uri;
    }
}
