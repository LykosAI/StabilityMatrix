using System.Collections.Generic;
using System.Text.Json.Serialization;
using LiteDB;

namespace StabilityMatrix.Models.Api;

public class CivitModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; }
    
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

    private FileSizeType? _fullFilesSize;
    public FileSizeType FullFilesSize
    {
        get
        {
            if (_fullFilesSize == null)
            {
                var kbs = 0.0;
                if (ModelVersions is not null)
                {
                    var latestVersion = ModelVersions[0];
                    if (latestVersion.Files is not null)
                    {
                        var latestModelFile = latestVersion.Files[0];
                        kbs = latestModelFile.SizeKb;
                    }
                }
                _fullFilesSize = new FileSizeType(kbs);
            }
            return _fullFilesSize;
        }
    }
}
