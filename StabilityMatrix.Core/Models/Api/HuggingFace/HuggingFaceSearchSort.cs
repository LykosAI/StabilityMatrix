using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Api.HuggingFace;

/// <summary>
/// Sort order for HuggingFace model search results.
/// </summary>
public enum HuggingFaceSearchSort
{
    [Description("Most downloads")]
    Downloads,

    [Description("Most likes")]
    Likes,

    [Description("Recently updated")]
    LastModified,
}
