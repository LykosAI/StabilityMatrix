using System;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class CivitFile
{
    [JsonPropertyName("sizeKb")]
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
}
