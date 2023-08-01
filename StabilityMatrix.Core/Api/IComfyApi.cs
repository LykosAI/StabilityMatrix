using Refit;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Core.Api;

[Headers("User-Agent: StabilityMatrix")]
public interface IComfyApi
{
    [Post("/prompt")]
    Task<ComfyPromptResponse> PostPrompt(
        [Body] ComfyPromptRequest prompt,
        CancellationToken cancellationToken = default
    );

    [Get("/history/{promptId}")]
    Task<Dictionary<string, ComfyHistoryResponse>> GetHistory(
        string promptId,
        CancellationToken cancellationToken = default
    );

    [Get("/view")]
    Task<Stream> GetImage(
        string filename,
        string subfolder,
        string type,
        CancellationToken cancellationToken = default
    );
}
