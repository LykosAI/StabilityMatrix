using System.Collections.Immutable;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Core.Helper;

/// <summary>
/// Record of remote model resources
/// </summary>
public static class RemoteModels
{
    /// <summary>
    /// Root of huggingface upscalers
    /// </summary>
    private static Uri UpscalersRoot { get; } = new("https://huggingface.co/LykosAI/Upscalers/");

    /// <summary>
    /// Root of huggingface upscalers at the main branch
    /// </summary>
    private static Uri UpscalersRootMain { get; } = UpscalersRoot.Append("blob/main/");

    public static IReadOnlyList<RemoteResource> Upscalers { get; } =
        new RemoteResource[]
        {
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x2plus.pth"
                ),
                HashSha256 = "49fafd45f8fd7aa8d31ab2a22d14d91b536c34494a5cfe31eb5d89c2fa266abb",
                InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                Author = "xinntao",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "RealESRGAN/LICENSE.txt"),
                ContextType = SharedFolderType.RealESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x2plus.pth"
                ),
                HashSha256 = "49fafd45f8fd7aa8d31ab2a22d14d91b536c34494a5cfe31eb5d89c2fa266abb",
                InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                Author = "xinntao",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "RealESRGAN/LICENSE.txt"),
                ContextType = SharedFolderType.RealESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus.pth"
                ),
                HashSha256 = "4fa0d38905f75ac06eb49a7951b426670021be3018265fd191d2125df9d682f1",
                InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                Author = "xinntao",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "RealESRGAN/LICENSE.txt"),
                ContextType = SharedFolderType.RealESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus_anime_6B.pth"
                ),
                HashSha256 = "f872d837d3c90ed2e05227bed711af5671a6fd1c9f7d7e91c911a61f155e99da",
                InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                Author = "xinntao",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "RealESRGAN/LICENSE.txt"),
                ContextType = SharedFolderType.RealESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/28a279ec60b5c47c9f39381dcaa997b9402ac09d/RealESRGAN/RealESRGAN_x4plus_anime_6B.pth"
                ),
                HashSha256 = "f872d837d3c90ed2e05227bed711af5671a6fd1c9f7d7e91c911a61f155e99da",
                InfoUrl = new Uri("https://github.com/xinntao/ESRGAN"),
                Author = "xinntao",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "RealESRGAN/LICENSE.txt"),
                ContextType = SharedFolderType.RealESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/4f813cc283de5cd66fe253bc05e66ca76bf68c51/SwinIR/SwinIR_4x.pth"
                ),
                HashSha256 = "99adfa91350a84c99e946c1eb3d8fce34bc28f57d807b09dc8fe40a316328c0a",
                InfoUrl = new Uri("https://github.com/JingyunLiang/SwinIR"),
                Author = "JingyunLiang",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(UpscalersRootMain, "SwinIR/LICENSE.txt"),
                ContextType = SharedFolderType.SwinIR
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Smooth.pth"
                ),
                HashSha256 = "7deeeac95ce7c28d616933b789f51642d169b200a6638edfb1c57ccecd903cd0",
                InfoUrl = new Uri("https://github.com/Kim2091"),
                Author = "Kim2091",
                LicenseType = "CC BY-NC-SA 4.0",
                LicenseUrl = new Uri(UpscalersRootMain, "UltraMix/LICENSE.txt"),
                ContextType = SharedFolderType.ESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Restore.pth"
                ),
                HashSha256 = "e8982b435557baeea5a08066aede41a9b3c8a6512c8688dab6d326e91ba82fa3",
                InfoUrl = new Uri("https://github.com/Kim2091"),
                Author = "Kim2091",
                LicenseType = "CC BY-NC-SA 4.0",
                LicenseUrl = new Uri(UpscalersRootMain, "UltraMix/LICENSE.txt"),
                ContextType = SharedFolderType.ESRGAN
            },
            new()
            {
                Url = UpscalersRoot.Append(
                    "resolve/6a1d697dd18f8c4ff031f26e6dc523ada419517d/UltraMix/4x-UltraMix_Balanced.pth"
                ),
                HashSha256 = "e23ca000107aae95ec9b8d7d1bf150f7884f1361cd9d669bdf824d72529f0e26",
                InfoUrl = new Uri("https://github.com/Kim2091"),
                Author = "Kim2091",
                LicenseType = "CC BY-NC-SA 4.0",
                LicenseUrl = new Uri(UpscalersRootMain, "UltraMix/LICENSE.txt"),
                ContextType = SharedFolderType.ESRGAN
            }
        };

    private static Uri ControlNetRoot { get; } = new("https://huggingface.co/lllyasviel/ControlNet/");

    private static RemoteResource ControlNetCommon(string path, string sha256)
    {
        const string commit = "38a62cbf79862c1bac73405ec8dc46133aee3e36";

        return new RemoteResource
        {
            Url = ControlNetRoot.Append($"resolve/{commit}/").Append(path),
            HashSha256 = sha256,
            InfoUrl = ControlNetRoot,
            Author = "lllyasviel",
            LicenseType = "OpenRAIL",
            LicenseUrl = ControlNetRoot,
            ContextType = SharedFolderType.ControlNet
        };
    }

    public static IReadOnlyList<RemoteResource> ControlNets { get; } =
        new[]
        {
            ControlNetCommon(
                "models/control_sd15_canny.pth",
                "4de384b16bc2d7a1fb258ca0cbd941d7dd0a721ae996aff89f905299d6923f45"
            ),
            ControlNetCommon(
                "models/control_sd15_depth.pth",
                "726cd0b472c4b5c0341b01afcb7fdc4a7b4ab7c37fe797fd394c9805cbef60bf"
            ),
            ControlNetCommon(
                "models/control_sd15_openpose.pth",
                "d19ffffeeaff6d9feb2204b234c3e1b9aec039ab3e63fca07f4fe5646f2ef591"
            )
        };

    public static HybridModelFile ControlNetReferenceOnlyModel { get; } =
        HybridModelFile.FromRemote("@ReferenceOnly");

    public static IReadOnlyList<HybridModelFile> ControlNetModels { get; } =
        ControlNets
            .Select(HybridModelFile.FromDownloadable)
            .Concat([ControlNetReferenceOnlyModel])
            .ToImmutableArray();

    private static IEnumerable<RemoteResource> PromptExpansions =>
        [
            new RemoteResource
            {
                Url = new Uri("https://cdn.lykos.ai/models/GPT-Prompt-Expansion-Fooocus-v2.zip"),
                HashSha256 = "82e69311787c0bb6736389710d80c0a2b653ed9bbe6ea6e70c6b90820fe42d88",
                InfoUrl = new Uri("https://huggingface.co/LykosAI/GPT-Prompt-Expansion-Fooocus-v2"),
                Author = "lllyasviel, LykosAI",
                LicenseType = "GPLv3",
                LicenseUrl = new Uri("https://github.com/lllyasviel/Fooocus/blob/main/LICENSE"),
                ContextType = SharedFolderType.PromptExpansion,
                AutoExtractArchive = true,
                ExtractRelativePath = "GPT-Prompt-Expansion-Fooocus-v2"
            }
        ];

    public static IEnumerable<HybridModelFile> PromptExpansionModels =>
        PromptExpansions.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> UltralyticsModels =>
        [
            new RemoteResource
            {
                Url = new Uri("https://huggingface.co/Bingsu/adetailer/resolve/main/face_yolov8m.pt"),
                HashSha256 = "f02b8a23e6f12bd2c1b1f6714f66f984c728fa41ed749d033e7d6dea511ef70c",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativePath = "bbox"
            },
            new RemoteResource
            {
                Url = new Uri("https://huggingface.co/Bingsu/adetailer/resolve/main/hand_yolov8s.pt"),
                HashSha256 = "5c4faf8d17286ace2c3d3346c6d0d4a0c8d62404955263a7ae95c1dd7eb877af",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativePath = "bbox"
            },
            new RemoteResource
            {
                Url = new Uri("https://huggingface.co/Bingsu/adetailer/resolve/main/person_yolov8m-seg.pt"),
                HashSha256 = "9d881ec50b831f546e37977081b18f4e3bf65664aec163f97a311b0955499795",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativePath = "segm"
            },
            new RemoteResource
            {
                Url = new Uri("https://huggingface.co/Bingsu/adetailer/resolve/main/person_yolov8s-seg.pt"),
                HashSha256 = "b5684835e79fd8b805459e0f7a0f9daa437e421cb4a214fff45ec4ac61767ef9",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativePath = "segm"
            }
        ];

    public static IEnumerable<HybridModelFile> UltralyticsModelFiles =>
        UltralyticsModels.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> SamModels =>
        [
            new RemoteResource
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams
            },
            new RemoteResource
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_h_4b8939.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams
            },
            new RemoteResource
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_l_0b3195.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams
            }
        ];

    public static IEnumerable<HybridModelFile> SamModelFiles =>
        SamModels.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> ClipModels =>
        [
            new RemoteResource
            {
                Url = new Uri(
                    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/clip_l.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
                HashSha256 = "660c6f5b1abae9dc498ac2d21e1347d2abdb0cf6c0c0c8576cd796491d9a6cdd",
                Author = "OpenAI",
                LicenseType = "MIT",
                ContextType = SharedFolderType.CLIP,
            },
            new RemoteResource
            {
                Url = new Uri(
                    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp16.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
                HashSha256 = "6e480b09fae049a72d2a8c5fbccb8d3e92febeb233bbe9dfe7256958a9167635",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.CLIP,
            },
            new RemoteResource
            {
                Url = new Uri(
                    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp8_e4m3fn.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
                HashSha256 = "7d330da4816157540d6bb7838bf63a0f02f573fc48ca4d8de34bb0cbfd514f09",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.CLIP,
            }
        ];

    public static IEnumerable<HybridModelFile> ClipModelFiles =>
        ClipModels.Select(HybridModelFile.FromDownloadable);
}
