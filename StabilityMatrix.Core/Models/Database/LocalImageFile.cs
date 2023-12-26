using DynamicData.Tests;
using MetadataExtractor.Formats.Exif;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.FileInterfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Size = System.Drawing.Size;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a locally indexed image file.
/// </summary>
public record LocalImageFile
{
    public required string AbsolutePath { get; init; }

    /// <summary>
    /// Type of the model file.
    /// </summary>
    public LocalImageFileType ImageType { get; init; }

    /// <summary>
    /// Creation time of the file.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Last modified time of the file.
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; init; }

    /// <summary>
    /// Generation parameters metadata of the file.
    /// </summary>
    public GenerationParameters? GenerationParameters { get; init; }

    /// <summary>
    /// Dimensions of the image
    /// </summary>
    public Size? ImageSize { get; init; }

    /// <summary>
    /// File name of the relative path.
    /// </summary>
    public string FileName => Path.GetFileName(AbsolutePath);

    /// <summary>
    /// File name of the relative path without extension.
    /// </summary>
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(AbsolutePath);

    public (string? Parameters, string? ParametersJson, string? SMProject, string? ComfyNodes) ReadMetadata()
    {
        if (AbsolutePath.EndsWith("webp"))
        {
            var paramsJson = ImageMetadata.ReadTextChunkFromWebp(
                AbsolutePath,
                ExifDirectoryBase.TagImageDescription
            );
            var smProj = ImageMetadata.ReadTextChunkFromWebp(AbsolutePath, ExifDirectoryBase.TagSoftware);

            return (null, paramsJson, smProj, null);
        }

        using var stream = new FileStream(AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        // TODO: Support other types
        const LocalImageFileType imageType = LocalImageFileType.Inference | LocalImageFileType.TextToImage;

        if (filePath.Extension.Contains("webp"))
        {
            var paramsJson = ImageMetadata.ReadTextChunkFromWebp(
                filePath,
                ExifDirectoryBase.TagImageDescription
            );
            var parameters = string.IsNullOrWhiteSpace(paramsJson)
                ? null
                : JsonSerializer.Deserialize<GenerationParameters>(paramsJson);

            filePath.Info.Refresh();

            return new LocalImageFile
            {
                AbsolutePath = filePath,
                ImageType = imageType,
                CreatedAt = filePath.Info.CreationTimeUtc,
                LastModifiedAt = filePath.Info.LastWriteTimeUtc,
                GenerationParameters = parameters,
                ImageSize = new Size(parameters?.Width ?? 0, parameters?.Height ?? 0)
            };
        }

        // Get metadata
        using var stream = filePath.Info.OpenRead();
        using var reader = new BinaryReader(stream);

        var imageSize = ImageMetadata.GetImageSize(reader);

        var metadata = ImageMetadata.ReadTextChunk(reader, "parameters-json");

        GenerationParameters? genParams;

        if (!string.IsNullOrWhiteSpace(metadata))
        {
            genParams = JsonSerializer.Deserialize<GenerationParameters>(metadata);
        }
        else
        {
            metadata = ImageMetadata.ReadTextChunk(reader, "parameters");
            GenerationParameters.TryParse(metadata, out genParams);
        }

        filePath.Info.Refresh();

        return new LocalImageFile
        {
            AbsolutePath = filePath,
            ImageType = imageType,
            CreatedAt = filePath.Info.CreationTimeUtc,
            LastModifiedAt = filePath.Info.LastWriteTimeUtc,
            GenerationParameters = genParams,
            ImageSize = imageSize
        };
    }

    public static readonly HashSet<string> SupportedImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp"
    ];
}
