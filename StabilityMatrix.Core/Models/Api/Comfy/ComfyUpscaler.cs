using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models.Api.Comfy;

public readonly record struct ComfyUpscaler(string Name, ComfyUpscalerType Type)
{
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
        ConvertDict.Keys
            .Select(k => new ComfyUpscaler(k, ComfyUpscalerType.Latent))
            .ToImmutableArray();

    [JsonIgnore]
    public string DisplayType
    {
        get
        {
            return Type switch
            {
                ComfyUpscalerType.Latent => "Latent",
                ComfyUpscalerType.ESRGAN => "ESRGAN",
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

            if (Type == ComfyUpscalerType.ESRGAN)
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
            if (Type == ComfyUpscalerType.ESRGAN)
            {
                // Remove file extensions
                return Path.GetFileNameWithoutExtension(Name);
            }

            return DisplayName;
        }
    }

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

    public static IEqualityComparer<ComfyUpscaler> Comparer { get; } =
        new NameTypeEqualityComparer();
}
