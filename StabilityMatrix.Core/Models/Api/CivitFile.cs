using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api;

public class CivitFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("sizeKB")]
    public double SizeKb { get; set; }

    [JsonPropertyName("pickleScanResult")]
    public string PickleScanResult { get; set; }

    [JsonPropertyName("virusScanResult")]
    public string VirusScanResult { get; set; }

    [JsonPropertyName("scannedAt")]
    public DateTime? ScannedAt { get; set; }

    [JsonPropertyName("metadata")]
    public CivitFileMetadata Metadata { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; }

    [JsonPropertyName("hashes")]
    public CivitFileHashes Hashes { get; set; }

    [JsonPropertyName("type")]
    public CivitFileType Type { get; set; }

    [JsonPropertyName("primary")]
    public bool IsPrimary { get; set; }

    private FileSizeType? fullFilesSize;
    public FileSizeType FullFilesSize
    {
        get
        {
            if (fullFilesSize != null)
                return fullFilesSize;
            fullFilesSize = new FileSizeType(SizeKb);
            return fullFilesSize;
        }
    }

    public string DisplayName => Path.GetFileNameWithoutExtension(Name);

    /// <summary>
    /// Gets <see cref="DownloadUrl"/> pinned to this specific file via a <c>fileId</c> query parameter.
    /// CivitAI resolves <c>/api/download/models/{versionId}</c> URLs without a <c>fileId</c> by
    /// preference-scoring the version's files against the metadata query parameters (format/size/fp),
    /// which serves a different file of the same version when several files share the same metadata.
    /// Pinning the file id makes the server look the file up directly instead.
    /// Non-CivitAI URLs (e.g. raw storage URLs from the tRPC fallback) are returned unchanged.
    /// </summary>
    public string GetFileSpecificDownloadUrl()
    {
        if (
            Id <= 0
            || string.IsNullOrEmpty(DownloadUrl)
            || !Uri.TryCreate(DownloadUrl, UriKind.Absolute, out var uri)
            || !uri.Host.Equals("civitai.com", StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith("/api/download/", StringComparison.OrdinalIgnoreCase)
            || uri.Query.Contains("fileId=", StringComparison.OrdinalIgnoreCase)
        )
        {
            return DownloadUrl;
        }

        var separator = string.IsNullOrEmpty(uri.Query) ? '?' : '&';
        return $"{DownloadUrl}{separator}fileId={Id}";
    }
}
