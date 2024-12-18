using System.Diagnostics.CodeAnalysis;
using LiteDB;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api;

namespace StabilityMatrix.Core.Models.Database;

/// <summary>
/// Represents a locally indexed model file.
/// </summary>
public record LocalModelFile
{
    private sealed class RelativePathConnectedModelInfoEqualityComparer : IEqualityComparer<LocalModelFile>
    {
        public bool Equals(LocalModelFile? x, LocalModelFile? y)
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
                && Equals(x.ConnectedModelInfo, y.ConnectedModelInfo)
                && x.HasUpdate == y.HasUpdate;
        }

        public int GetHashCode(LocalModelFile obj)
        {
            return HashCode.Combine(obj.RelativePath, obj.ConnectedModelInfo, obj.HasUpdate);
        }
    }

    public static IEqualityComparer<LocalModelFile> RelativePathConnectedModelInfoComparer { get; } =
        new RelativePathConnectedModelInfoEqualityComparer();

    public virtual bool Equals(LocalModelFile? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return RelativePath == other.RelativePath
            && Equals(ConnectedModelInfo, other.ConnectedModelInfo)
            && HasUpdate == other.HasUpdate;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(RelativePath, ConnectedModelInfo, HasUpdate);
    }

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
    /// The latest CivitModel info
    /// </summary>
    [BsonRef("CivitModels")]
    public CivitModel? LatestModelInfo { get; set; }

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

    public string? HashSha256 => ConnectedModelInfo?.Hashes.SHA256;

    [BsonIgnore]
    public string? PreviewImageFullPathGlobal =>
        PreviewImageFullPath ?? GetPreviewImageFullPath(GlobalConfig.ModelsDir);

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

    [BsonIgnore]
    [MemberNotNullWhen(true, nameof(ConnectedModelInfo))]
    public bool HasConnectedModel => ConnectedModelInfo != null;

    [BsonIgnore]
    [MemberNotNullWhen(true, nameof(ConnectedModelInfo))]
    public bool HasCivitMetadata => HasConnectedModel && ConnectedModelInfo.ModelId != null;

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

    public string GetConnectedModelInfoFullPath(string rootModelDirectory)
    {
        var modelNameNoExt = Path.GetFileNameWithoutExtension(RelativePath);
        var modelParentDir = Path.GetDirectoryName(GetFullPath(rootModelDirectory)) ?? "";
        return Path.Combine(modelParentDir, $"{modelNameNoExt}.cm-info.json");
    }

    public IEnumerable<string> GetDeleteFullPaths(string rootModelDirectory)
    {
        if (GetFullPath(rootModelDirectory) is { } filePath && File.Exists(filePath))
        {
            yield return filePath;
        }

        if (
            HasConnectedModel
            && GetConnectedModelInfoFullPath(rootModelDirectory) is { } cmInfoPath
            && File.Exists(cmInfoPath)
        )
        {
            yield return cmInfoPath;
        }

        var previewImagePath = GetPreviewImageFullPath(rootModelDirectory);
        if (File.Exists(previewImagePath))
        {
            yield return previewImagePath;
        }
    }

    public static readonly HashSet<string> SupportedCheckpointExtensions =
    [
        ".safetensors",
        ".pt",
        ".ckpt",
        ".pth",
        ".bin",
        ".sft",
        ".gguf"
    ];
    public static readonly HashSet<string> SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly HashSet<string> SupportedMetadataExtensions = [".json"];
}
