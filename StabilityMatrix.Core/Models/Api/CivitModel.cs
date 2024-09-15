using System.Text.Json.Serialization;
using LiteDB;

namespace StabilityMatrix.Core.Models.Api;

public class CivitModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public CivitModelType Type { get; set; }

    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; }

    [JsonPropertyName("mode")]
    public CivitMode? Mode { get; set; }

    [JsonPropertyName("creator")]
    public CivitCreator Creator { get; set; }

    [JsonPropertyName("stats")]
    public CivitModelStats Stats { get; set; }

    [BsonRef("ModelVersions")]
    [JsonPropertyName("modelVersions")]
    public List<CivitModelVersion>? ModelVersions { get; set; }

    private FileSizeType? fullFilesSize;
    public FileSizeType FullFilesSize
    {
        get
        {
            if (fullFilesSize != null)
                return fullFilesSize;
            var kbs = 0.0;

            var latestVersion = ModelVersions?.FirstOrDefault();
            if (latestVersion?.Files != null && latestVersion.Files.Any())
            {
                var latestModelFile = latestVersion.Files.FirstOrDefault(x => x.Type == CivitFileType.Model);
                kbs = latestModelFile?.SizeKb ?? 0;
            }
            fullFilesSize = new FileSizeType(kbs);
            return fullFilesSize;
        }
    }

    public string LatestModelVersionName =>
        ModelVersions is { Count: > 0 } ? ModelVersions[0].Name : string.Empty;

    public string? BaseModelType =>
        ModelVersions is { Count: > 0 } ? ModelVersions[0].BaseModel?.Replace("SD", "").Trim() : string.Empty;

    public CivitModelStats ModelVersionStats =>
        ModelVersions is { Count: > 0 } ? ModelVersions[0].Stats : new CivitModelStats();

    public string LatestVersionCreatedAt =>
        (
            ModelVersions is { Count: > 0 }
                ? ModelVersions[0].PublishedAt ?? DateTimeOffset.MinValue
                : DateTimeOffset.MinValue
        ).ToString("M/d/yy");
}
