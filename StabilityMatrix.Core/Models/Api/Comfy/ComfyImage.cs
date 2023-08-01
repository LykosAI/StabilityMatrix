using System.Text.Json.Serialization;
using System.Web;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public class ComfyImage
{
    [JsonPropertyName("filename")]
    public required string FileName { get; set; }
    
    [JsonPropertyName("subfolder")]
    public required string SubFolder { get; set; }
    
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    public Uri ToUri(Uri baseAddress)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["filename"] = FileName;
        query["subfolder"] = SubFolder;
        query["type"] = Type;
        
        return new UriBuilder(baseAddress)
        {
            Path = "/view",
            Query = query.ToString()
        }.Uri;
    }
}
