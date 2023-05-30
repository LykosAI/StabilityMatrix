using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class GithubBranch
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("commit")]
    public GithubCommit Commit { get; set; }
}
