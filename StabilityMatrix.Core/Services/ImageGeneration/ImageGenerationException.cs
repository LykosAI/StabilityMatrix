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
}
