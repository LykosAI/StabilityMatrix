using System.Threading.Tasks;
using Refit;
using StabilityMatrix.Models.Api;

namespace StabilityMatrix.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IA3WebApi
{
    [Get("/internal/ping")]
    Task<string> GetPing();
    
    [Post("/sdapi/v1/txt2img")]
    Task<ImageResponse> TextToImage([Body] TextToImageRequest request);
    
    [Get("/sdapi/v1/progress")]
    Task<ProgressResponse> GetProgress([Body] ProgressRequest request);
    
    [Get("/sdapi/v1/options")]
    Task<A3Options> GetOptions();
    
    [Post("/sdapi/v1/options")]
    Task SetOptions([Body] A3Options request);
}
