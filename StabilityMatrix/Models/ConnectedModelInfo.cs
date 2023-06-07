using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
    public DateTimeOffset ImportedAt { get; set; }
    public CivitFileHashes Hashes { get; set; }

    // User settings
    public string? UserTitle { get; set; }
    public string? ThumbnailImageUrl { get; set; }
    
    public ConnectedModelInfo()
    {
    }
    
    public ConnectedModelInfo(CivitModel civitModel, CivitModelVersion civitModelVersion, CivitFile civitFile, DateTimeOffset importedAt)
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
        return JsonSerializer.Deserialize<ConnectedModelInfo>(json, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull } );
    }

    /// <summary>
    /// Saves the model info to a json file in the specified directory.
    /// Overwrites existing files.
    /// </summary>
    /// <param name="directoryPath">Path of directory to save file</param>
    /// <param name="modelFileName">Model file name without extensions</param>
    public async Task SaveJsonToDirectory(string directoryPath, string modelFileName)
    {
        var name = modelFileName + ".cm-info.json";
        var json = JsonSerializer.Serialize(this);
        await File.WriteAllTextAsync(Path.Combine(directoryPath, name), json);
    }
}
