using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class GithubCommit
{
    [JsonPropertyName("sha")]
    public string ShaHash { get; set; }
    
    [JsonPropertyName("commit")]
    public GithubCommitCommit? Commit { get; set; }
}
