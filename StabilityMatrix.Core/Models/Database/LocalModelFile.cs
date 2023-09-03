using LiteDB;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a locally indexed model file.
/// </summary>
public class LocalModelFile
{
    /// <summary>
    /// Relative path to the file from the root model directory.
    /// </summary>
    [BsonId]
    public required string RelativePath { get; set; }

    /// <summary>
    /// Type of the model file.
    /// </summary>
    public required SharedFolderType SharedFolderType { get; set; }

    /// <summary>
    /// Optional connected model information.
    /// </summary>
    public ConnectedModelInfo? ConnectedModelInfo { get; set; }
    
    /// <summary>
    /// Optional preview image relative path.
    /// </summary>
    public string? PreviewImageRelativePath { get; set; }

    public string GetFullPath(string rootModelDirectory)
    {
        return Path.Combine(rootModelDirectory, RelativePath);
    }
    
    public string? GetPreviewImageFullPath(string rootModelDirectory)
    {
        return PreviewImageRelativePath == null ? null 
            : Path.Combine(rootModelDirectory, PreviewImageRelativePath);
    }

    public static readonly HashSet<string> SupportedCheckpointExtensions =
        new() { ".safetensors", ".pt", ".ckpt", ".pth", ".bin" };
    public static readonly HashSet<string> SupportedImageExtensions =
        new() { ".png", ".jpg", ".jpeg" };
    public static readonly HashSet<string> SupportedMetadataExtensions = new() { ".json" };
}
