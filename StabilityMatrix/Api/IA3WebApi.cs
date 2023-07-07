using System.Threading;
using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IA3WebApi
{
    [Get("/internal/ping")]
    Task<string> GetPing(CancellationToken cancellationToken = default);
    
    [Post("/sdapi/v1/txt2img")]
    Task<ImageResponse> TextToImage([Body] TextToImageRequest request, CancellationToken cancellationToken = default);
    
    [Get("/sdapi/v1/progress")]
    Task<ProgressResponse> GetProgress([Body] ProgressRequest request, CancellationToken cancellationToken = default);
    
    [Get("/sdapi/v1/options")]
    Task<A3Options> GetOptions(CancellationToken cancellationToken = default);
    
    [Post("/sdapi/v1/options")]
    Task SetOptions([Body] A3Options request, CancellationToken cancellationToken = default);
}
