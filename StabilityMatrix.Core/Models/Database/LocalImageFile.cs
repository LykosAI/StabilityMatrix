using System.Text.Json;
using DynamicData.Tests;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;
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

    public (
        string? Parameters,
        string? ParametersJson,
        string? SMProject,
        string? ComfyNodes,
        string? CivitParameters
    ) ReadMetadata()
    {
        if (AbsolutePath.EndsWith("webp"))
        {
            var paramsJson = ImageMetadata.ReadTextChunkFromWebp(
                AbsolutePath,
                ExifDirectoryBase.TagImageDescription
            );
            var smProj = ImageMetadata.ReadTextChunkFromWebp(AbsolutePath, ExifDirectoryBase.TagSoftware);

            return (null, paramsJson, smProj, null, null);
        }

        using var stream = new FileStream(AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        var parameters = ImageMetadata.ReadTextChunk(reader, "parameters");
        var parametersJson = ImageMetadata.ReadTextChunk(reader, "parameters-json");
        var smProject = ImageMetadata.ReadTextChunk(reader, "smproj");
        var comfyNodes = ImageMetadata.ReadTextChunk(reader, "prompt");
        var civitParameters = ImageMetadata.ReadTextChunk(reader, "user_comment");

        return (
            string.IsNullOrEmpty(parameters) ? null : parameters,
            string.IsNullOrEmpty(parametersJson) ? null : parametersJson,
            string.IsNullOrEmpty(smProject) ? null : smProject,
            string.IsNullOrEmpty(comfyNodes) ? null : comfyNodes,
            string.IsNullOrEmpty(civitParameters) ? null : civitParameters
        );
    }

    public static LocalImageFile FromPath(FilePath filePath)
    {
        // TODO: Support other types
        const LocalImageFileType imageType = LocalImageFileType.Inference | LocalImageFileType.TextToImage;

        if (filePath.Extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var paramsJson = ImageMetadata.ReadTextChunkFromWebp(
                filePath,
                ExifDirectoryBase.TagImageDescription
            );

            GenerationParameters? parameters = null;
            try
            {
                parameters = string.IsNullOrWhiteSpace(paramsJson)
                    ? null
                    : JsonSerializer.Deserialize<GenerationParameters>(paramsJson);
            }
            catch (JsonException)
            {
                // just don't load params I guess, no logger here <_<
            }

            filePath.Info.Refresh();

            return new LocalImageFile
            {
                AbsolutePath = filePath,
                ImageType = imageType,
                CreatedAt = filePath.Info.CreationTimeUtc,
                LastModifiedAt = filePath.Info.LastWriteTimeUtc,
                GenerationParameters = parameters,
                ImageSize = new Size(parameters?.Width ?? 0, parameters?.Height ?? 0),
            };
        }

        if (filePath.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
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
                if (string.IsNullOrWhiteSpace(metadata)) // if still empty, try civitai metadata (user_comment)
                {
                    metadata = ImageMetadata.ReadTextChunk(reader, "user_comment");
                }
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
                ImageSize = imageSize,
            };
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var ms = new SKManagedStream(fs);
        var codec = SKCodec.Create(ms);

        return new LocalImageFile
        {
            AbsolutePath = filePath,
            ImageType = imageType,
            CreatedAt = filePath.Info.CreationTimeUtc,
            LastModifiedAt = filePath.Info.LastWriteTimeUtc,
            ImageSize = new Size { Height = codec.Info.Height, Width = codec.Info.Width },
        };
    }

    public static readonly HashSet<string> SupportedImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
    ];
}
