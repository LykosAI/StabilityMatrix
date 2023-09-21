using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
    /// Static root of huggingface upscalers
    /// </summary>
    private static Uri HuggingFaceRoot { get; } = new("https://huggingface.co/LykosAI/Upscalers/");

    /// <summary>
    /// Default remote downloadable models
    /// </summary>
    public static IReadOnlyList<ComfyUpscaler> DefaultDownloadableModels { get; } =
        new[]
        {
            new ComfyUpscaler("RealESRGAN_x2plus.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x2plus.pth"
                    ),
                    "49fafd45f8fd7aa8d31ab2a22d14d91b536c34494a5cfe31eb5d89c2fa266abb"
                )
                {
                    InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                    Author = "xinntao",
                    LicenseType = "Apache 2.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "RealESRGAN/LICENSE.txt"),
                    ContextType = SharedFolderType.RealESRGAN
                }
            },
            new ComfyUpscaler("RealESRGAN_x4plus.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus.pth"
                    ),
                    "4fa0d38905f75ac06eb49a7951b426670021be3018265fd191d2125df9d682f1"
                )
                {
                    InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                    Author = "xinntao",
                    LicenseType = "Apache 2.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "RealESRGAN/LICENSE.txt"),
                    ContextType = SharedFolderType.RealESRGAN
                }
            },
            new ComfyUpscaler("RealESRGAN_x4plus_anime_6B.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus_anime_6B.pth"
                    ),
                    "f872d837d3c90ed2e05227bed711af5671a6fd1c9f7d7e91c911a61f155e99da"
                )
                {
                    InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                    Author = "xinntao",
                    LicenseType = "Apache 2.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "RealESRGAN/LICENSE.txt"),
                    ContextType = SharedFolderType.RealESRGAN
                }
            },
            new ComfyUpscaler("RealESRGAN_x4plus_anime_6B.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus_anime_6B.pth"
                    ),
                    "f872d837d3c90ed2e05227bed711af5671a6fd1c9f7d7e91c911a61f155e99da"
                )
                {
                    InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                    Author = "xinntao",
                    LicenseType = "Apache 2.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "RealESRGAN/LICENSE.txt"),
                    ContextType = SharedFolderType.RealESRGAN
                }
            },
            new ComfyUpscaler("SwinIR_4x.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/4f813cc283de5cd66fe253bc05e66ca76bf68c51/SwinIR/SwinIR_4x.pth"
                    ),
                    "99adfa91350a84c99e946c1eb3d8fce34bc28f57d807b09dc8fe40a316328c0a"
                )
                {
                    InfoUrl = new Uri("https://github.com/JingyunLiang/SwinIR"),
                    Author = "JingyunLiang",
                    LicenseType = "Apache 2.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "SwinIR/LICENSE.txt"),
                    ContextType = SharedFolderType.SwinIR
                }
            },
            new ComfyUpscaler("4x-UltraMix_Smooth.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Smooth.pth"
                    ),
                    "7deeeac95ce7c28d616933b789f51642d169b200a6638edfb1c57ccecd903cd0"
                )
                {
                    InfoUrl = new Uri("https://github.com/Kim2091"),
                    Author = "Kim2091",
                    LicenseType = "CC BY-NC-SA 4.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "UltraMix/LICENSE.txt"),
                    ContextType = SharedFolderType.ESRGAN
                }
            },
            new ComfyUpscaler("4x-UltraMix_Restore.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Restore.pth"
                    ),
                    "e8982b435557baeea5a08066aede41a9b3c8a6512c8688dab6d326e91ba82fa3"
                )
                {
                    InfoUrl = new Uri("https://github.com/Kim2091"),
                    Author = "Kim2091",
                    LicenseType = "CC BY-NC-SA 4.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "UltraMix/LICENSE.txt"),
                    ContextType = SharedFolderType.ESRGAN
                }
            },
            new ComfyUpscaler("4x-UltraMix_Balanced.pth", ComfyUpscalerType.DownloadableModel)
            {
                DownloadableResource = new RemoteResource(
                    new Uri(
                        HuggingFaceRoot,
                        "/resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Balanced.pth"
                    ),
                    "e23ca000107aae95ec9b8d7d1bf150f7884f1361cd9d669bdf824d72529f0e26"
                )
                {
                    InfoUrl = new Uri("https://github.com/Kim2091"),
                    Author = "Kim2091",
                    LicenseType = "CC BY-NC-SA 4.0",
                    LicenseUrl = new Uri(HuggingFaceRoot, "UltraMix/LICENSE.txt"),
                    ContextType = SharedFolderType.ESRGAN
                }
            }
        };

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
