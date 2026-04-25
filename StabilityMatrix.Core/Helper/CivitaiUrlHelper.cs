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
