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

    [Post("/interrupt")]
    Task PostInterrupt(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload an image to Comfy
    /// </summary>
    /// <param name="image">Image as StreamPart</param>
    /// <param name="overwrite">Whether to overwrite at destination</param>
    /// <param name="type">One of "input", "temp", "output"</param>
    /// <param name="subfolder">Subfolder</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    [Multipart]
    [Post("/upload/image")]
    Task<ComfyUploadImageResponse> PostUploadImage(
        StreamPart image,
        string? overwrite = null,
        string? type = null,
        string? subfolder = null,
        CancellationToken cancellationToken = default
    );

    [Get("/history/{promptId}")]
    Task<Dictionary<string, ComfyHistoryResponse>> GetHistory(
        string promptId,
        CancellationToken cancellationToken = default
    );

    [Get("/object_info/{nodeType}")]
    Task<Dictionary<string, ComfyObjectInfo>> GetObjectInfo(
        string nodeType,
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
