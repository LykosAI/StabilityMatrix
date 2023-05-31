using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class GithubCommitCommit
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    [JsonPropertyName("author")] 
    public GithubAuthor Author { get; set; }
}
