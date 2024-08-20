using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public readonly record struct ComfyUpscaler(string Name, ComfyUpscalerType Type) : IDownloadableResource
{
    public static ComfyUpscaler NearestExact { get; } = new("nearest-exact", ComfyUpscalerType.Latent);

    private static Dictionary<string, string> ConvertDict { get; } =
        new()
        {
            ["nearest-exact"] = "Nearest Exact",
            ["bilinear"] = "Bilinear",
            ["area"] = "Area",
            ["bicubic"] = "Bicubic",
            ["bislerp"] = "Bislerp",
        };

    public static IReadOnlyList<ComfyUpscaler> Defaults { get; } =
        ConvertDict.Keys.Select(k => new ComfyUpscaler(k, ComfyUpscalerType.Latent)).ToImmutableArray();

    public static ComfyUpscaler FromDownloadable(RemoteResource resource)
    {
        return new ComfyUpscaler(resource.FileName, ComfyUpscalerType.DownloadableModel)
        {
            DownloadableResource = resource
        };
    }

    /// <summary>
    /// Downloadable model information.
    /// If this is set, <see cref="Type"/> should be <see cref="ComfyUpscalerType.DownloadableModel"/>.
    /// </summary>
    public RemoteResource? DownloadableResource { get; init; }

    [MemberNotNullWhen(true, nameof(DownloadableResource))]
    public bool IsDownloadable => DownloadableResource != null;

    [JsonIgnore]
    public string DisplayType
    {
        get
        {
            return Type switch
            {
                ComfyUpscalerType.Latent => "Latent",
                ComfyUpscalerType.ESRGAN => "ESRGAN",
                ComfyUpscalerType.DownloadableModel => "Downloadable",
                ComfyUpscalerType.None => "None",
                _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
            };
        }
    }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (Type == ComfyUpscalerType.Latent)
            {
                return ConvertDict.TryGetValue(Name, out var displayName) ? displayName : Name;
            }

            if (Type is ComfyUpscalerType.ESRGAN or ComfyUpscalerType.DownloadableModel)
            {
                // Remove file extensions
                return Path.GetFileNameWithoutExtension(Name);
            }

            return Name;
        }
    }

    [JsonIgnore]
    public string ShortDisplayName
    {
        get
        {
            if (Type != ComfyUpscalerType.Latent)
            {
                // Remove file extensions
                return Path.GetFileNameWithoutExtension(Name);
            }

            return DisplayName;
        }
    }

    /// <summary>
    /// Default remote downloadable models
    /// </summary>
    public static IReadOnlyList<ComfyUpscaler> DefaultDownloadableModels { get; } =
        RemoteModels.Upscalers.Select(FromDownloadable).ToImmutableArray();

    private sealed class NameTypeEqualityComparer : IEqualityComparer<ComfyUpscaler>
    {
        public bool Equals(ComfyUpscaler x, ComfyUpscaler y)
        {
            return x.Name == y.Name && x.Type == y.Type;
        }

        public int GetHashCode(ComfyUpscaler obj)
        {
            return HashCode.Combine(obj.Name, (int)obj.Type);
        }
    }

    public static IEqualityComparer<ComfyUpscaler> Comparer { get; } = new NameTypeEqualityComparer();
}
