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
}
