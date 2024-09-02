using LiteDB;

namespace StabilityMatrix.Core.Models.Database;

public class PyPiCacheEntry
{
    [BsonId]
    public required string CacheKey { get; set; }
    public required List<CustomVersion> Versions { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
