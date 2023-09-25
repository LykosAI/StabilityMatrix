using LiteDB;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Size = System.Drawing.Size;

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
    /// Generation parameters metadata of the file.
    /// </summary>
    public GenerationParameters? GenerationParameters { get; set; }

    /// <summary>
    /// Dimensions of the image
    /// </summary>
    public Size? ImageSize { get; set; }

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

    public (
        string? Parameters,
        string? ParametersJson,
        string? SMProject,
        string? ComfyNodes
    ) ReadMetadata()
    {
        using var stream = new FileStream(
            GlobalFullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        using var reader = new BinaryReader(stream);

        var parameters = ImageMetadata.ReadTextChunk(reader, "parameters");
        var parametersJson = ImageMetadata.ReadTextChunk(reader, "parameters-json");
        var smProject = ImageMetadata.ReadTextChunk(reader, "smproj");
        var comfyNodes = ImageMetadata.ReadTextChunk(reader, "prompt");

        return (
            string.IsNullOrEmpty(parameters) ? null : parameters,
            string.IsNullOrEmpty(parametersJson) ? null : parametersJson,
            string.IsNullOrEmpty(smProject) ? null : smProject,
            string.IsNullOrEmpty(comfyNodes) ? null : comfyNodes
        );
    }

    public static LocalImageFile FromPath(FilePath filePath)
    {
        var relativePath = Path.GetRelativePath(
            GlobalConfig.LibraryDir.JoinDir("Images"),
            filePath
        );

        // TODO: Support other types
        const LocalImageFileType imageType =
            LocalImageFileType.Inference | LocalImageFileType.TextToImage;

        // Get metadata
        using var stream = new FileStream(
            filePath.FullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        using var reader = new BinaryReader(stream);

        var imageSize = ImageMetadata.GetImageSize(reader);

        var metadata = ImageMetadata.ReadTextChunk(reader, "parameters-json");

        GenerationParameters? genParams = null;

        if (!string.IsNullOrWhiteSpace(metadata))
        {
            genParams = JsonSerializer.Deserialize<GenerationParameters>(metadata);
        }
        else
        {
            metadata = ImageMetadata.ReadTextChunk(reader, "parameters");
            GenerationParameters.TryParse(metadata, out genParams);
        }

        return new LocalImageFile
        {
            RelativePath = relativePath,
            ImageType = imageType,
            CreatedAt = filePath.Info.CreationTimeUtc,
            LastModifiedAt = filePath.Info.LastWriteTimeUtc,
            GenerationParameters = genParams,
            ImageSize = imageSize
        };
    }

    public static readonly HashSet<string> SupportedImageExtensions =
        new() { ".png", ".jpg", ".jpeg", ".webp" };

    private sealed class LocalImageFileEqualityComparer : IEqualityComparer<LocalImageFile>
    {
        public bool Equals(LocalImageFile? x, LocalImageFile? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (ReferenceEquals(x, null))
                return false;
            if (ReferenceEquals(y, null))
                return false;
            if (x.GetType() != y.GetType())
                return false;
            return x.RelativePath == y.RelativePath
                && x.ImageType == y.ImageType
                && x.CreatedAt.Equals(y.CreatedAt)
                && x.LastModifiedAt.Equals(y.LastModifiedAt)
                && Equals(x.GenerationParameters, y.GenerationParameters);
        }

        public int GetHashCode(LocalImageFile obj)
        {
            return HashCode.Combine(
                obj.RelativePath,
                obj.ImageType,
                obj.CreatedAt,
                obj.LastModifiedAt,
                obj.GenerationParameters
            );
        }
    }

    public static IEqualityComparer<LocalImageFile> Comparer { get; } =
        new LocalImageFileEqualityComparer();
}
