using Refit;
using StabilityMatrix.Core.Models.Api.OpenArt;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IOpenArtApi
{
    [Get("/feed")]
    Task<OpenArtSearchResponse> GetFeedAsync([Query] OpenArtFeedRequest request);

    [Get("/list")]
    Task<OpenArtSearchResponse> SearchAsync([Query] OpenArtFeedRequest request);

    [Post("/download")]
    Task<OpenArtDownloadResponse> DownloadWorkflowAsync([Body] OpenArtDownloadRequest request);
}
