using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Database;

namespace StabilityMatrix.Core.Models;

/// <summary>
/// Model file union that may be remote or local.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public record HybridModelFile
{
    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents use of a default model.
    /// </summary>
    public static HybridModelFile Default { get; } = FromRemote("@default");

    /// <summary>
    /// Singleton instance of <see cref="HybridModelFile"/> that represents no model.
    /// </summary>
    public static HybridModelFile None { get; } = FromRemote("@none");

    private string? RemoteName { get; init; }

    public LocalModelFile? Local { get; init; }

    /// <summary>
    /// Downloadable model information.
    /// </summary>
    public RemoteResource? DownloadableResource { get; init; }

    public HybridModelType Type { get; init; }

    [MemberNotNullWhen(true, nameof(RemoteName))]
    [MemberNotNullWhen(false, nameof(Local))]
    [JsonIgnore]
    public bool IsRemote => RemoteName != null;

    [MemberNotNullWhen(true, nameof(DownloadableResource))]
    public bool IsDownloadable => DownloadableResource != null;

    [JsonIgnore]
    public string RelativePath => IsRemote ? RemoteName : Local.RelativePathFromSharedFolder;

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

            return Path.GetFileNameWithoutExtension(RelativePath);
        }
    }

    private HybridModelFile() { }

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
        return new HybridModelFile
        {
            DownloadableResource = resource,
            Type = HybridModelType.Downloadable
        };
    }

    public string GetId()
    {
        return $"{RelativePath};{IsNone};{IsDefault}";
    }

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

            return Equals(x.RelativePath, y.RelativePath)
                && x.IsNone == y.IsNone
                && x.IsDefault == y.IsDefault;
        }

        public int GetHashCode(HybridModelFile obj)
        {
            return HashCode.Combine(obj.IsNone, obj.IsDefault, obj.RelativePath);
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

    public static IEqualityComparer<HybridModelFile> Comparer { get; } =
        new RemoteNameLocalEqualityComparer();
}
