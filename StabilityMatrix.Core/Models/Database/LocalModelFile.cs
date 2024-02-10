using LiteDB;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a locally indexed model file.
/// </summary>
public record LocalModelFile
{
    /// <summary>
    /// Relative path to the file from the root model directory.
    /// </summary>
    [BsonId]
    public required string RelativePath { get; init; }

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

    /// <summary>
    /// Optional preview image full path. Takes priority over <see cref="PreviewImageRelativePath"/>.
    /// </summary>
    public string? PreviewImageFullPath { get; set; }

    /// <summary>
    /// Optional full path to the model's configuration (.yaml) file.
    /// </summary>
    public string? ConfigFullPath { get; set; }

    /// <summary>
    /// Whether or not an update is available for this model
    /// </summary>
    public bool HasUpdate { get; set; }

    /// <summary>
    /// Last time this model was checked for an update
    /// </summary>
    public DateTimeOffset LastUpdateCheck { get; set; }

    /// <summary>
    /// File name of the relative path.
    /// </summary>
    [BsonIgnore]
    public string FileName => Path.GetFileName(RelativePath);

    /// <summary>
    /// File name of the relative path without extension.
    /// </summary>
    [BsonIgnore]
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(RelativePath);

    /// <summary>
    /// Relative file path from the shared folder type model directory.
    /// </summary>
    [BsonIgnore]
    public string RelativePathFromSharedFolder =>
        Path.GetRelativePath(SharedFolderType.GetStringValue(), RelativePath);

    /// <summary>
    /// Blake3 hash of the file.
    /// </summary>
    public string? HashBlake3 => ConnectedModelInfo?.Hashes.BLAKE3;

    [BsonIgnore]
    public string FullPathGlobal => GetFullPath(GlobalConfig.LibraryDir.JoinDir("Models"));

    [BsonIgnore]
    public string? PreviewImageFullPathGlobal =>
        PreviewImageFullPath ?? GetPreviewImageFullPath(GlobalConfig.LibraryDir.JoinDir("Models"));

    [BsonIgnore]
    public Uri? PreviewImageUriGlobal =>
        PreviewImageFullPathGlobal == null ? null : new Uri(PreviewImageFullPathGlobal);

    [BsonIgnore]
    public string DisplayModelName => ConnectedModelInfo?.ModelName ?? FileNameWithoutExtension;

    [BsonIgnore]
    public string DisplayModelVersion => ConnectedModelInfo?.VersionName ?? string.Empty;

    [BsonIgnore]
    public string DisplayModelFileName => FileName;

    [BsonIgnore]
    public string DisplayConfigFileName => Path.GetFileName(ConfigFullPath) ?? string.Empty;

    public string GetFullPath(string rootModelDirectory)
    {
        return Path.Combine(rootModelDirectory, RelativePath);
    }

    public string? GetPreviewImageFullPath(string rootModelDirectory)
    {
        if (PreviewImageFullPath != null)
            return PreviewImageFullPath;

        return PreviewImageRelativePath == null
            ? null
            : Path.Combine(rootModelDirectory, PreviewImageRelativePath);
    }

    /*protected bool Equals(LocalModelFile other)
    {
        return RelativePath == other.RelativePath;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != this.GetType())
            return false;
        return Equals((LocalModelFile)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return RelativePath.GetHashCode();
    }*/

    public static readonly HashSet<string> SupportedCheckpointExtensions =
    [
        ".safetensors",
        ".pt",
        ".ckpt",
        ".pth",
        ".bin"
    ];
    public static readonly HashSet<string> SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly HashSet<string> SupportedMetadataExtensions = [".json"];
}
