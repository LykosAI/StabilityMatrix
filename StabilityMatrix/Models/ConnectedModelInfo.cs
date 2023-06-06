using System;
using System.Text.Json;
using StabilityMatrix.Extensions;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Models;

public class ConnectedModelInfo
{
    public int ModelId { get; set; }
    public string ModelName { get; set; }
    public string ModelDescription { get; set; }
    public bool Nsfw { get; set; }
    public string[] Tags { get; set; }
    public CivitModelType ModelType { get; set; }
    public int VersionId { get; set; }
    public string VersionName { get; set; }
    public string VersionDescription { get; set; }
    public string? BaseModel { get; set; }
    public CivitFileMetadata FileMetadata { get; set; }
    public DateTime ImportedAt { get; set; }
    public CivitFileHashes Hashes { get; set; }

    // User settings
    public string? UserTitle { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    
    public ConnectedModelInfo(CivitModel civitModel, CivitModelVersion civitModelVersion, CivitFile civitFile, DateTime importedAt)
    {
        ModelId = civitModel.Id;
        ModelName = civitModel.Name;
        ModelDescription = civitModel.Description;
        Nsfw = civitModel.Nsfw;
        Tags = civitModel.Tags;
        ModelType = civitModel.Type;
        VersionId = civitModelVersion.Id;
        VersionName = civitModelVersion.Name;
        VersionDescription = civitModelVersion.Description;
        ImportedAt = importedAt;
        BaseModel = civitModelVersion.BaseModel;
        FileMetadata = civitFile.Metadata;
        Hashes = civitFile.Hashes;
    }
    
    public static ConnectedModelInfo? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ConnectedModelInfo>(json);
    }
}
