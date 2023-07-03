using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public record CivitModelVersionResponse(
    [property: JsonPropertyName("id")]
    int Id,
    
    [property: JsonPropertyName("modelId")]
    int ModelId,
    
    [property: JsonPropertyName("name")]
    string Name,
    
    [property: JsonPropertyName("baseModel")]
    string BaseModel,
    
    [property: JsonPropertyName("files")]
    List<CivitFile> Files,
    
    [property: JsonPropertyName("images")]
    List<CivitImage> Images,
    
    [property: JsonPropertyName("downloadUrl")]
    string DownloadUrl
);
