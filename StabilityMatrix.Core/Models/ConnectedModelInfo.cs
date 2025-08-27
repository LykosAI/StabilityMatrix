using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.OpenModelsDb;

namespace StabilityMatrix.Core.Models;

public class ConnectedModelInfo : IEquatable<ConnectedModelInfo>
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
    public InferenceDefaults? InferenceDefaults { get; set; }

    public ConnectedModelSource? Source { get; set; } = ConnectedModelSource.Civitai;

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
        Source = ConnectedModelSource.Civitai;
    }

    public ConnectedModelInfo(
        CivitModel civitModel,
        CivitModelVersion civitModelVersion,
        CivitFile civitFile,
        DateTimeOffset importedAt,
        InferenceDefaults? inferenceDefaults
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
        Source = ConnectedModelSource.Civitai;
        InferenceDefaults = inferenceDefaults;
    }

    public ConnectedModelInfo(
        OpenModelDbKeyedModel model,
        OpenModelDbResource resource,
        DateTimeOffset importedAt
    )
    {
        ModelName = model.Id;
        ModelDescription = model.Description ?? string.Empty;
        VersionName = model.Name ?? string.Empty;
        Tags = model.Tags?.ToArray() ?? [];
        ImportedAt = importedAt;
        Hashes = new CivitFileHashes { SHA256 = resource.Sha256 };
        ThumbnailImageUrl = resource.Urls?.FirstOrDefault();
        ModelType = CivitModelType.Upscaler;
        Source = ConnectedModelSource.OpenModelDb;
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

    public bool Equals(ConnectedModelInfo? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Comparer.Equals(this, other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((ConnectedModelInfo)obj);
    }

    public override int GetHashCode()
    {
        return Comparer.GetHashCode(this);
    }

    public static bool operator ==(ConnectedModelInfo? left, ConnectedModelInfo? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ConnectedModelInfo? left, ConnectedModelInfo? right)
    {
        return !Equals(left, right);
    }

    private sealed class ConnectedModelInfoEqualityComparer : IEqualityComparer<ConnectedModelInfo>
    {
        public bool Equals(ConnectedModelInfo? x, ConnectedModelInfo? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null)
                return false;
            if (y is null)
                return false;
            if (x.GetType() != y.GetType())
                return false;

            return x.ModelId == y.ModelId
                && x.ModelName == y.ModelName
                && x.ModelDescription == y.ModelDescription
                && x.Nsfw == y.Nsfw
                && x.Tags?.SequenceEqual(y.Tags ?? []) is null or true
                && x.ModelType == y.ModelType
                && x.VersionId == y.VersionId
                && x.VersionName == y.VersionName
                && x.VersionDescription == y.VersionDescription
                && x.BaseModel == y.BaseModel
                && x.FileMetadata == y.FileMetadata
                && x.ImportedAt.Equals(y.ImportedAt)
                && x.Hashes == y.Hashes
                && x.TrainedWords?.SequenceEqual(y.TrainedWords ?? []) is null or true
                && x.Stats == y.Stats
                && x.UserTitle == y.UserTitle
                && x.ThumbnailImageUrl == y.ThumbnailImageUrl
                && x.InferenceDefaults == y.InferenceDefaults
                && x.Source == y.Source;
        }

        public int GetHashCode(ConnectedModelInfo obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.ModelId);
            hashCode.Add(obj.ModelName);
            hashCode.Add(obj.ModelDescription);
            hashCode.Add(obj.Nsfw);
            hashCode.Add(obj.Tags);
            hashCode.Add((int)obj.ModelType);
            hashCode.Add(obj.VersionId);
            hashCode.Add(obj.VersionName);
            hashCode.Add(obj.VersionDescription);
            hashCode.Add(obj.BaseModel);
            hashCode.Add(obj.FileMetadata);
            hashCode.Add(obj.ImportedAt);
            hashCode.Add(obj.Hashes);
            hashCode.Add(obj.TrainedWords);
            hashCode.Add(obj.Stats);
            hashCode.Add(obj.UserTitle);
            hashCode.Add(obj.ThumbnailImageUrl);
            hashCode.Add(obj.InferenceDefaults);
            hashCode.Add(obj.Source);
            return hashCode.ToHashCode();
        }
    }

    public static IEqualityComparer<ConnectedModelInfo> Comparer { get; } =
        new ConnectedModelInfoEqualityComparer();
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(ConnectedModelInfo))]
internal partial class ConnectedModelInfoSerializerContext : JsonSerializerContext;
