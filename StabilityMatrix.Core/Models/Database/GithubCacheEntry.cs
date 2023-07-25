using LiteDB;
using Octokit;

namespace StabilityMatrix.Core.Models.Database;

public class GithubCacheEntry
{
    [BsonId]
    public string CacheKey { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public IEnumerable<Release> AllReleases { get; set; } = new List<Release>();
    public IEnumerable<Branch> Branches { get; set; } = new List<Branch>();
    public IEnumerable<GitCommit> Commits { get; set; } = new List<GitCommit>();
}
