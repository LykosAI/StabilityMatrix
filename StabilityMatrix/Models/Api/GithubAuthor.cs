using System;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

public class GithubAuthor
{
    [JsonPropertyName("name")] 
    public string Name { get; set; }
    
    [JsonPropertyName("date")]
    public DateTimeOffset DateCommitted { get; set; }
}
