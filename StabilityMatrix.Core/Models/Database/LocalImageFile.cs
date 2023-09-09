using LiteDB;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a locally indexed image file.
/// </summary>
public class LocalImageFile
{
    /// <summary>
    /// Relative path of the file from the root images directory ("%LIBRARY%/Images").
    /// </summary>
    [BsonId]
    public required string RelativePath { get; set; }

    /// <summary>
    /// Type of the model file.
    /// </summary>
    public LocalImageFileType ImageType { get; set; }

    /// <summary>
    /// Creation time of the file.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last modified time of the file.
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; set; }

    /// <summary>
    /// File name of the relative path.
    /// </summary>
    public string FileName => Path.GetFileName(RelativePath);

    /// <summary>
    /// File name of the relative path without extension.
    /// </summary>
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(RelativePath);

    public string GlobalFullPath =>
        GlobalConfig.LibraryDir.JoinDir("Images").JoinFile(RelativePath);

    public string GetFullPath(string rootImageDirectory)
    {
        return Path.Combine(rootImageDirectory, RelativePath);
    }

    public static readonly HashSet<string> SupportedImageExtensions =
        new() { ".png", ".jpg", ".jpeg", ".webp" };
}
