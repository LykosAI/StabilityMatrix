namespace StabilityMatrix.Core.Helper;

public static class CivitaiUrlHelper
{
    public const string SafeHost = "civitai.com";
    public const string MatureHost = "civitai.red";

    public static string GetModelUrl(int modelId, bool isNsfw, int? modelVersionId = null)
    {
        var baseUrl = $"https://{GetHost(isNsfw)}/models/{modelId}";
        return modelVersionId is > 0 ? $"{baseUrl}?modelVersionId={modelVersionId}" : baseUrl;
    }

    /// <summary>
    /// Returns a CivitAI CDN image url that delivers at most <paramref name="maxWidth"/> pixels wide.
    /// The API returns <c>original=true</c> urls (full-resolution PNGs, often several MB) and those are
    /// swapped for a <c>width=</c> transform; an existing wider <c>width=</c> transform is capped.
    /// Urls from other hosts are returned unchanged.
    /// </summary>
    public static string CapImageWidth(string url, int maxWidth)
    {
        if (!url.Contains("image.civitai.com", StringComparison.OrdinalIgnoreCase))
            return url;

        url = url.Replace("original=true", $"width={maxWidth}", StringComparison.OrdinalIgnoreCase);

        return System.Text.RegularExpressions.Regex.Replace(url, @"width=(\d+)", match =>
            int.TryParse(match.Groups[1].Value, out var width) && width > maxWidth
                ? $"width={maxWidth}"
                : match.Value
        );
    }

    public static bool TryParseModelId(string? url, out int modelId)
    {
        modelId = 0;

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (
            !uri.Host.Equals(SafeHost, StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals(MatureHost, StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals($"www.{SafeHost}", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals($"www.{MatureHost}", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 2
            && segments[0].Equals("models", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[1], out modelId);
    }

    private static string GetHost(bool isNsfw)
    {
        return isNsfw ? MatureHost : SafeHost;
    }
}
