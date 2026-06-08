using System.Collections.Generic;

namespace StabilityMatrix.Core.Models.Api.CivArchive;

public class CivArchiveSearchFilters
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<string> Types { get; set; } = [];
    public IReadOnlyList<string> BaseModels { get; set; } = [];
    public CivArchivePlatformOption Platform { get; set; } = CivArchivePlatformOption.All;
    public CivArchiveSortOption Sort { get; set; } = CivArchiveSortOption.Top;
    public CivArchiveRatingOption Rating { get; set; } = CivArchiveRatingOption.Safe;
    public CivArchivePlatformStatusOption PlatformStatus { get; set; } = CivArchivePlatformStatusOption.All;
    public CivArchiveKindOption Kind { get; set; } = CivArchiveKindOption.All;
    public string Tags { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public CivArchivePeriodOption Period { get; set; } = CivArchivePeriodOption.All;
    public int Page { get; set; } = 1;
    public string RoutePath { get; set; } = "/top-models";
}
