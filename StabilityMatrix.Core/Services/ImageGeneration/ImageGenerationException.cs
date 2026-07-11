namespace StabilityMatrix.Core.Services.ImageGeneration;

/// <summary>
/// Exception thrown when image generation fails
/// </summary>
public class ImageGenerationException : Exception
{
    public ImageGenerationException(string message)
        : base(message) { }

    public ImageGenerationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Raw machine-readable error detail (e.g. ComfyUI's JSON body for a rejected workflow
    /// or node execution error), carried through from the provider so the UI can offer a
    /// detail dialog alongside the short message.
    /// </summary>
    public string? DetailJson { get; init; }

    public ImageGenerationErrorCode? ErrorCode { get; init; }
}
