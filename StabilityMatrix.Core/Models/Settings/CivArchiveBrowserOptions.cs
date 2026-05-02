using System.Collections.Generic;
using StabilityMatrix.Core.Models.Api.CivArchive;

namespace StabilityMatrix.Core.Models.Settings;

public class CivArchiveBrowserOptions
{
    public string Query { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public CivArchivePlatformOption Platform { get; set; } = CivArchivePlatformOption.All;
    public CivArchiveSortOption Sort { get; set; } = CivArchiveSortOption.Top;
    public CivArchivePeriodOption Period { get; set; } = CivArchivePeriodOption.All;
    public CivArchiveRatingOption Rating { get; set; } = CivArchiveRatingOption.Safe;
    public CivArchivePlatformStatusOption PlatformStatus { get; set; } = CivArchivePlatformStatusOption.All;
    public CivArchiveKindOption Kind { get; set; } = CivArchiveKindOption.All;
    public List<string> SelectedModelTypes { get; set; } = [];
    public List<string> SelectedBaseModels { get; set; } = [];
}
