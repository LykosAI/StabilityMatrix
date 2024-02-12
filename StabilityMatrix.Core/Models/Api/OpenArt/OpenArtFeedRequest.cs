using Refit;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

/// <summary>
/// Note that parameters Category, Custom Node and Sort should be used separately
/// </summary>
public class OpenArtFeedRequest
{
    [AliasAs("category")]
    public string Category { get; set; }

    [AliasAs("sort")]
    public string Sort { get; set; }

    [AliasAs("custom_node")]
    public string CustomNode { get; set; }

    [AliasAs("cursor")]
    public string Cursor { get; set; }
}
