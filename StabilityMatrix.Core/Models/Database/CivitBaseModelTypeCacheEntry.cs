namespace StabilityMatrix.Core.Models.Database;

public class CivitBaseModelTypeCacheEntry
{
    public required string Id { get; set; }
    public List<string> ModelTypes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
