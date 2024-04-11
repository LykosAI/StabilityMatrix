using Refit;

namespace StabilityMatrix.Core.Models.Api.OpenArt;

public class OpenArtSearchRequest
{
    [AliasAs("keyword")]
    public required string Keyword { get; set; }

    [AliasAs("pageSize")]
    public int PageSize { get; set; } = 30;

    /// <summary>
    /// 0-based index of the page to retrieve
    /// </summary>
    [AliasAs("currentPage")]
    public int CurrentPage { get; set; } = 0;
}
