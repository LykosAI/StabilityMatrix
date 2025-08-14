using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Model file union that may be remote or local.
/// </summary>
[Localizable(false)]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public record HybridModelFile : ISearchText, IDownloadableResource
{
    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents use of a default model.
    /// </summary>
    public static HybridModelFile Default { get; } = FromRemote("@default");

    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents no model.
    /// </summary>
    public static HybridModelFile None { get; } = FromRemote("@none");

    public string? RemoteName { get; init; }

    public LocalModelFile? Local { get; init; }

    /// <summary>
    /// Downloadable model information.
    /// </summary>
    public RemoteResource? DownloadableResource { get; init; }

    public HybridModelType Type { get; init; }

    [MemberNotNullWhen(true, nameof(RemoteName))]
    [JsonIgnore]
    public bool IsRemote => RemoteName != null;

    [MemberNotNullWhen(true, nameof(DownloadableResource))]
    public bool IsDownloadable => DownloadableResource != null;

    [JsonIgnore]
    public string RelativePath =>
        Type switch
        {
            HybridModelType.Local => Local!.RelativePathFromSharedFolder,
            HybridModelType.Remote => RemoteName!,
            HybridModelType.Downloadable => DownloadableResource!.Value.RelativeDirectory == null
                ? DownloadableResource!.Value.FileName
                : Path.Combine(
                    DownloadableResource!.Value.RelativeDirectory,
                    DownloadableResource!.Value.FileName
                ),
            HybridModelType.None => throw new InvalidOperationException(),
            _ => throw new ArgumentOutOfRangeException(),
        };

    [JsonIgnore]
    public string FileName => Path.GetFileName(RelativePath);

    [JsonIgnore]
    public string ShortDisplayName
    {
        get
        {
            if (IsNone)
            {
                return "None";
            }

            if (IsDefault)
            {
                return "Default";
            }

            if (ReferenceEquals(this, RemoteModels.ControlNetReferenceOnlyModel))
            {
                return "Reference Only";
            }

            var fileName = Path.GetFileNameWithoutExtension(RelativePath);

            if (
                !fileName.Equals("diffusion_pytorch_model", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals("pytorch_model", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals("ip_adapter", StringComparison.OrdinalIgnoreCase)
            )
            {
                return Path.GetFileNameWithoutExtension(RelativePath);
            }

            // show a friendlier name when models have the same name like ip_adapter or diffusion_pytorch_model
            var directoryName = Path.GetDirectoryName(RelativePath);
            if (directoryName is null)
                return Path.GetFileNameWithoutExtension(RelativePath);

            var lastIndex = directoryName.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastIndex < 0)
                return $"{fileName} ({directoryName})";

            var parentDirectoryName = directoryName.Substring(lastIndex + 1);
            return $"{fileName} ({parentDirectoryName})";
        }
    }

    [JsonIgnore]
    public string SortKey =>
        Local?.ConnectedModelInfo != null
            ? $"{Local.ConnectedModelInfo.ModelName}{Local.ConnectedModelInfo.VersionName}"
            : ShortDisplayName;

    public static HybridModelFile FromLocal(LocalModelFile local)
    {
        return new HybridModelFile { Local = local, Type = HybridModelType.Local };
    }

    public static HybridModelFile FromRemote(string remoteName)
    {
        return new HybridModelFile { RemoteName = remoteName, Type = HybridModelType.Remote };
    }

    public static HybridModelFile FromDownloadable(RemoteResource resource)
    {
        return new HybridModelFile { DownloadableResource = resource, Type = HybridModelType.Downloadable };
    }

    public string GetId()
    {
        return $"{RelativePath.NormalizePathSeparators()};{IsNone};{IsDefault}";
    }

    /// <summary>
    /// Special Comparer that compares Remote Name and Local RelativePath,
    /// used for letting remote models not override local models with more metadata.
    /// Pls do not use for other stuff.
    /// </summary>
    private sealed class RemoteNameLocalEqualityComparer : IEqualityComparer<HybridModelFile>
    {
        public bool Equals(HybridModelFile? x, HybridModelFile? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (ReferenceEquals(x, null))
                return false;
            if (ReferenceEquals(y, null))
                return false;
            if (x.GetType() != y.GetType())
                return false;

            if (!Equals(x.RelativePath.NormalizePathSeparators(), y.RelativePath.NormalizePathSeparators()))
                return false;

            // This equality affects replacements of remote over local models
            // We want local and remote models to be considered equal if they have the same relative path
            // But 2 local models with the same path but different config paths should be considered different
            return !(x.Type == y.Type && x.Local?.ConfigFullPath != y.Local?.ConfigFullPath);
        }

        public int GetHashCode(HybridModelFile obj)
        {
            return HashCode.Combine(obj.IsNone, obj.IsDefault, obj.RelativePath);
        }
    }

    /// <summary>
    /// Actual general purpose equality comparer.
    /// Use this for general equality checks :)
    /// </summary>
    private sealed class EqualityComparer : IEqualityComparer<HybridModelFile>
    {
        public bool Equals(HybridModelFile? x, HybridModelFile? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (ReferenceEquals(x, null))
                return false;
            if (ReferenceEquals(y, null))
                return false;
            if (x.GetType() != y.GetType())
                return false;

            if (!Equals(x.RelativePath.NormalizePathSeparators(), y.RelativePath.NormalizePathSeparators()))
                return false;

            return Equals(x.Type, y.Type)
                && x.RemoteName == y.RemoteName
                && x.Local?.ConfigFullPath == y.Local?.ConfigFullPath
                && x.Local?.ConnectedModelInfo == y.Local?.ConnectedModelInfo;
        }

        public int GetHashCode(HybridModelFile obj)
        {
            return HashCode.Combine(
                obj.IsNone,
                obj.IsDefault,
                obj.RelativePath,
                obj.RemoteName,
                obj.Local?.ConfigFullPath,
                obj.Local?.ConnectedModelInfo
            );
        }
    }

    /// <summary>
    /// Whether this instance is the default model.
    /// </summary>
    public bool IsDefault => ReferenceEquals(this, Default);

    /// <summary>
    /// Whether this instance is no model.
    /// </summary>
    public bool IsNone => ReferenceEquals(this, None);

    /// <summary>
    /// Actual general purpose equality comparer.
    /// Use this for general equality checks :)
    /// </summary>
    public static IEqualityComparer<HybridModelFile> Comparer { get; } = new EqualityComparer();

    /// <summary>
    /// Special Comparer that compares Remote Name and Local RelativePath,
    /// used for letting remote models not override local models with more metadata.
    /// Pls do not use for other stuff.
    /// </summary>
    public static IEqualityComparer<HybridModelFile> RemoteLocalComparer { get; } =
        new RemoteNameLocalEqualityComparer();

    [JsonIgnore]
    public string SearchText => SortKey;
}
