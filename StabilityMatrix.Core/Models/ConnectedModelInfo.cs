using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Models;

public class ConnectedModelInfo
{
    [JsonIgnore]
    public const string FileExtension = ".cm-info.json";

    public int? ModelId { get; set; }
    public string ModelName { get; set; }
    public string ModelDescription { get; set; }
    public bool Nsfw { get; set; }
    public string[] Tags { get; set; }
    public CivitModelType ModelType { get; set; }
    public int? VersionId { get; set; }
    public string VersionName { get; set; }
    public string? VersionDescription { get; set; }
    public string? BaseModel { get; set; }
    public CivitFileMetadata? FileMetadata { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public CivitFileHashes Hashes { get; set; }
    public string[]? TrainedWords { get; set; }
    public CivitModelStats? Stats { get; set; }

    // User settings
    public string? UserTitle { get; set; }
    public string? ThumbnailImageUrl { get; set; }

    public ConnectedModelInfo() { }

    public ConnectedModelInfo(
        CivitModel civitModel,
        CivitModelVersion civitModelVersion,
        CivitFile civitFile,
        DateTimeOffset importedAt
    )
    {
        ModelId = civitModel.Id;
        ModelName = civitModel.Name;
        ModelDescription = civitModel.Description ?? string.Empty;
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
        TrainedWords = civitModelVersion.TrainedWords;
        Stats = civitModel.Stats;
    }

    public static ConnectedModelInfo? FromJson(string json)
    {
        return JsonSerializer.Deserialize<ConnectedModelInfo>(
            json,
            new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }
        );
    }

    /// <summary>
    /// Saves the model info to a json file in the specified directory.
    /// Overwrites existing files.
    /// </summary>
    /// <param name="directoryPath">Path of directory to save file</param>
    /// <param name="modelFileName">Model file name without extensions</param>
    public async Task SaveJsonToDirectory(string directoryPath, string modelFileName)
    {
        var name = modelFileName + FileExtension;
        var json = JsonSerializer.Serialize(this);
        await File.WriteAllTextAsync(Path.Combine(directoryPath, name), json);
    }

    [JsonIgnore]
    public string TrainedWordsString => TrainedWords != null ? string.Join(", ", TrainedWords) : string.Empty;
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(ConnectedModelInfo))]
internal partial class ConnectedModelInfoSerializerContext : JsonSerializerContext;
