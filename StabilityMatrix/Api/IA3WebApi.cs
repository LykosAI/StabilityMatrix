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
}
