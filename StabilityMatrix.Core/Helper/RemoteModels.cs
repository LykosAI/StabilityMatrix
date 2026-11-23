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
                ContextType = SharedFolderType.RealESRGAN,
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
                ContextType = SharedFolderType.RealESRGAN,
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
                ContextType = SharedFolderType.RealESRGAN,
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
                ContextType = SharedFolderType.RealESRGAN,
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
                ContextType = SharedFolderType.RealESRGAN,
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
                ContextType = SharedFolderType.SwinIR,
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
                ContextType = SharedFolderType.ESRGAN,
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
                ContextType = SharedFolderType.ESRGAN,
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
                ContextType = SharedFolderType.ESRGAN,
            },
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
            ContextType = SharedFolderType.ControlNet,
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
            ),
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
            new()
            {
                Url = new Uri("https://cdn.lykos.ai/models/GPT-Prompt-Expansion-Fooocus-v2.zip"),
                HashSha256 = "82e69311787c0bb6736389710d80c0a2b653ed9bbe6ea6e70c6b90820fe42d88",
                InfoUrl = new Uri("https://huggingface.co/LykosAI/GPT-Prompt-Expansion-Fooocus-v2"),
                Author = "lllyasviel, LykosAI",
                LicenseType = "GPLv3",
                LicenseUrl = new Uri("https://github.com/lllyasviel/Fooocus/blob/main/LICENSE"),
                ContextType = SharedFolderType.PromptExpansion,
                AutoExtractArchive = true,
                ExtractRelativePath = "GPT-Prompt-Expansion-Fooocus-v2",
            },
        ];

    public static IEnumerable<HybridModelFile> PromptExpansionModels =>
        PromptExpansions.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> UltralyticsModels =>
        [
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Bingsu/adetailer/resolve/b0a075fd35454c86bb453a1ca06b29ffee704c20/face_yolov8m.pt"
                ),
                HashSha256 = "f02b8a23e6f12bd2c1b1f6714f66f984c728fa41ed749d033e7d6dea511ef70c",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativeDirectory = "bbox",
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Bingsu/adetailer/resolve/b0a075fd35454c86bb453a1ca06b29ffee704c20/hand_yolov8s.pt"
                ),
                HashSha256 = "5c4faf8d17286ace2c3d3346c6d0d4a0c8d62404955263a7ae95c1dd7eb877af",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativeDirectory = "bbox",
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Bingsu/adetailer/resolve/b0a075fd35454c86bb453a1ca06b29ffee704c20/person_yolov8m-seg.pt"
                ),
                HashSha256 = "9d881ec50b831f546e37977081b18f4e3bf65664aec163f97a311b0955499795",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativeDirectory = "segm",
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Bingsu/adetailer/resolve/b0a075fd35454c86bb453a1ca06b29ffee704c20/person_yolov8s-seg.pt"
                ),
                HashSha256 = "b5684835e79fd8b805459e0f7a0f9daa437e421cb4a214fff45ec4ac61767ef9",
                InfoUrl = new Uri("https://huggingface.co/Bingsu/adetailer"),
                Author = "Bingsu",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md"
                ),
                ContextType = SharedFolderType.Ultralytics,
                RelativeDirectory = "segm",
            },
        ];

    public static IEnumerable<HybridModelFile> UltralyticsModelFiles =>
        UltralyticsModels.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> SamModels =>
        [
            new()
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams,
            },
            new()
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_h_4b8939.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams,
            },
            new()
            {
                Url = new Uri("https://dl.fbaipublicfiles.com/segment_anything/sam_vit_l_0b3195.pth"),
                InfoUrl = new Uri("https://github.com/facebookresearch/segment-anything"),
                Author = "Facebook Research",
                LicenseType = "Apache 2.0",
                LicenseUrl = new Uri(
                    "https://github.com/facebookresearch/segment-anything/blob/main/LICENSE"
                ),
                ContextType = SharedFolderType.Sams,
            },
        ];

    public static IEnumerable<HybridModelFile> SamModelFiles =>
        SamModels.Select(HybridModelFile.FromDownloadable);

    /// <summary>
    /// Text encoders offered as cloud-download entries in the Inference encoder dropdowns.
    /// Companion encoders reference the shared statics above; the extra t5xxl / umt5
    /// precision variants exist only here (they aren't any profile's official companion).
    /// </summary>
    private static IEnumerable<RemoteResource> ClipModels =>
        [
            ClipLTextEncoder,
            T5XxlFp8ScaledTextEncoder,
            Qwen25Vl7bTextEncoder,
            Qwen3_4bTextEncoder,
            Qwen3_8bTextEncoder,
            Qwen3_06bBaseTextEncoder,
            Qwen3Vl4bTextEncoder,
            Qwen35_9bTextEncoder,
            ClipLHiDreamTextEncoder,
            ClipGHiDreamTextEncoder,
            Llama31_8bInstructTextEncoder,
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp16.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
                HashSha256 = "6e480b09fae049a72d2a8c5fbccb8d3e92febeb233bbe9dfe7256958a9167635",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.TextEncoders,
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp8_e4m3fn.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
                HashSha256 = "7d330da4816157540d6bb7838bf63a0f02f573fc48ca4d8de34bb0cbfd514f09",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.TextEncoders,
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/text_encoders/umt5_xxl_fp8_e4m3fn_scaled.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged"),
                HashSha256 = "c3355d30191f1f066b26d93fba017ae9809dce6c627dda5f6a66eaa651204f68",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.TextEncoders,
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/text_encoders/umt5_xxl_fp16.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged"),
                HashSha256 = "7b8850f1961e1cf8a77cca4c964a358d303f490833c6c087d0cff4b2f99db2af",
                Author = "Google",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.TextEncoders,
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/MiniMax-H3/resolve/main/text_encoders/qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/MiniMax-H3"),
                HashSha256 = "35a88d51044231fe332301d7a62aa81e3f2cba62febeb446e2c1e3e0ef76f2c6",
                Author = "MiniMax",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.TextEncoders,
            },
        ];

    public static IEnumerable<HybridModelFile> ClipModelFiles =>
        ClipModels.Select(HybridModelFile.FromDownloadable);

    /// <summary>
    /// Downloadable VAEs offered in the Inference VAE dropdowns; families whose VAEs aren't
    /// bundled in checkpoints (MiniMax H3's video + audio pair).
    /// </summary>
    private static IEnumerable<RemoteResource> VaeModels =>
        [
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/MiniMax-H3/resolve/main/vae/minimax_h3_video_vae_fp16.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/MiniMax-H3"),
                HashSha256 = "7c1f131492e7eddacaac9069a61b81bdd39de5cc96561e677c5eab1cdce5e522",
                Author = "MiniMax",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.VAE,
            },
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/MiniMax-H3/resolve/main/vae/minimax_h3_audio_vae_fp32.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/MiniMax-H3"),
                HashSha256 = "8e505d95dd1561d47abd43d4238fd40d9bb1ae9e147ed0a4cba778d76ae4db48",
                Author = "MiniMax",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.VAE,
            },
        ];

    public static IEnumerable<HybridModelFile> VaeModelFiles =>
        VaeModels.Select(HybridModelFile.FromDownloadable);

    private static IEnumerable<RemoteResource> ClipVisionModels =>
        [
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/clip_vision/clip_vision_h.safetensors"
                ),
                InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged"),
                HashSha256 = "64a7ef761bfccbadbaa3da77366aac4185a6c58fa5de5f589b42a65bcc21f161",
                Author = "OpenAI",
                LicenseType = "MIT",
                ContextType = SharedFolderType.ClipVision,
            },
            // ViT-H image encoder used by most IPAdapter models (SD1.5 and SDXL vit-h variants)
            new()
            {
                Url = new Uri(
                    "https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors"
                ),
                FileNameOverride = "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors",
                InfoUrl = new Uri("https://huggingface.co/h94/IP-Adapter"),
                HashSha256 = "6ca9667da1ca9e0b0f75e46bb030f7e011f44f86cbfb8d5a36590fcd7507b030",
                Author = "h94",
                LicenseType = "Apache 2.0",
                ContextType = SharedFolderType.ClipVision,
            },
        ];

    public static IEnumerable<HybridModelFile> ClipVisionModelFiles =>
        ClipVisionModels.Select(HybridModelFile.FromDownloadable);

    private static Uri IpAdapterRoot { get; } = new("https://huggingface.co/h94/IP-Adapter/");

    private static RemoteResource IpAdapterCommon(string path, string sha256) =>
        new()
        {
            Url = IpAdapterRoot.Append("resolve/main/").Append(path),
            HashSha256 = sha256,
            InfoUrl = IpAdapterRoot,
            Author = "h94",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.IpAdapter,
        };

    private static IEnumerable<RemoteResource> IpAdapters =>
        [
            IpAdapterCommon(
                "sdxl_models/ip-adapter_sdxl_vit-h.safetensors",
                "ebf05d918348aec7abb02a5e9ecef77e0aaea6914a5c4ea13f50d45eb1681831"
            ),
            IpAdapterCommon(
                "sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors",
                "3f5062b8400c94b7159665b21ba5c62acdcd7682262743d7f2aefedef00e6581"
            ),
            IpAdapterCommon(
                "sdxl_models/ip-adapter-plus-face_sdxl_vit-h.safetensors",
                "677ad8860204f7d0bfba12d29e6c31ded9beefdf3e4bbd102518357d31a292c1"
            ),
            IpAdapterCommon(
                "models/ip-adapter_sd15.safetensors",
                "289b45f16d043d0bf542e45831f971dcdaabe18b656f11e86d9dfba7e9ee3369"
            ),
            IpAdapterCommon(
                "models/ip-adapter-plus_sd15.safetensors",
                "a1c250be40455cc61a43da1201ec3f1edaea71214865fb47f57927e06cbe4996"
            ),
        ];

    public static IEnumerable<HybridModelFile> IpAdapterModelFiles =>
        IpAdapters.Select(HybridModelFile.FromDownloadable);

    #region Shared model components

    /// <summary>
    /// Individually addressable text encoders and VAEs shared across model families. Each is
    /// stated once here and referenced by the provider download sets and the Inference
    /// companion-download offers, so a URL can never drift between the two.
    /// </summary>
    public static RemoteResource FluxVae { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/Lumina_Image_2.0_Repackaged/resolve/main/split_files/vae/ae.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/black-forest-labs/FLUX.1-dev"),
            Author = "Black Forest Labs",
            LicenseType = "FLUX.1 [dev] Non-Commercial",
            LicenseUrl = new Uri("https://huggingface.co/black-forest-labs/FLUX.1-dev/blob/main/LICENSE.md"),
            ContextType = SharedFolderType.VAE,
        };

    public static RemoteResource ClipLTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/clip_l.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
            HashSha256 = "660c6f5b1abae9dc498ac2d21e1347d2abdb0cf6c0c0c8576cd796491d9a6cdd",
            Author = "OpenAI",
            LicenseType = "MIT",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource T5XxlFp8ScaledTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/comfyanonymous/flux_text_encoders/resolve/main/t5xxl_fp8_e4m3fn_scaled.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/comfyanonymous/flux_text_encoders"),
            HashSha256 = "a498f0485dc9536735258018417c3fd7758dc3bccc0a645feaa472b34955557a",
            Author = "Google",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource QwenImageVae { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/vae/qwen_image_vae.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI"),
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.VAE,
        };

    public static RemoteResource Qwen25Vl7bTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI"),
            HashSha256 = "cb5636d852a0ea6a9075ab1bef496c0db7aef13c02350571e388aea959c5c0b4",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Flux2Vae { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/flux2-klein-4B/resolve/main/split_files/vae/flux2-vae.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/flux2-klein-4B"),
            Author = "Black Forest Labs",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.VAE,
        };

    public static RemoteResource Qwen3_4bTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/flux2-klein-4B/resolve/main/split_files/text_encoders/qwen_3_4b.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/flux2-klein-4B"),
            HashSha256 = "6c671498573ac2f7a5501502ccce8d2b08ea6ca2f661c458e708f36b36edfc5a",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Qwen3_8bTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/flux2-klein-9B/resolve/main/split_files/text_encoders/qwen_3_8b.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/flux2-klein-9B"),
            HashSha256 = "f0ff9239d56269ca1d05e5f86da6a79fac111af464955681f11c7ab0ec5ef6c1",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Qwen3_06bBaseTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/circlestone-labs/Anima/resolve/main/split_files/text_encoders/qwen_3_06b_base.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/circlestone-labs/Anima"),
            HashSha256 = "cd2a512003e2f9f3cd3c32a9c3573f820bb28c940f73c57b1ddaa983d9223eba",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Qwen3Vl4bTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/Krea-2/resolve/main/text_encoders/qwen3vl_4b_fp8_scaled.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Krea-2"),
            HashSha256 = "54bd5144df0bbc25dd6ccadfcb826b521445a1b06ae5a42570bdd2974ca87094",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Qwen35_9bTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/MOSS-Image/resolve/main/text_encoders/qwen3.5_9b_w4a8_gptq.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/MOSS-Image"),
            HashSha256 = "4cc6a1543ad2061806da46f35f90cadb4801dcba246c44782cf42ec0feb6407c",
            Author = "Alibaba Qwen",
            LicenseType = "Apache 2.0",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource ClipLHiDreamTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI/resolve/main/split_files/text_encoders/clip_l_hidream.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI"),
            HashSha256 = "706fdb88e22e18177b207837c02f4b86a652abca0302821f2bfa24ac6aea4f71",
            Author = "OpenAI",
            LicenseType = "MIT",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource ClipGHiDreamTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI/resolve/main/split_files/text_encoders/clip_g_hidream.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI"),
            HashSha256 = "3771e70e36450e5199f30bad61a53faae85a2e02606974bcda0a6a573c0519d5",
            Author = "LAION",
            LicenseType = "MIT",
            ContextType = SharedFolderType.TextEncoders,
        };

    public static RemoteResource Llama31_8bInstructTextEncoder { get; } =
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI/resolve/main/split_files/text_encoders/llama_3.1_8b_instruct_fp8_scaled.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/HiDream-I1_ComfyUI"),
            HashSha256 = "9f86897bbeb933ef4fd06297740edb8dd962c94efcd92b373a11460c33765ea6",
            Author = "Meta",
            LicenseType = "Llama 3.1 Community License",
            ContextType = SharedFolderType.TextEncoders,
        };

    #endregion

    #region Flux Kontext Models

    /// <summary>
    /// All required models for Flux Kontext image editing
    /// </summary>
    public static IReadOnlyList<RemoteResource> FluxKontextModels { get; } =
    [
        // Flux Kontext UNET model (~11.9 GB)
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/flux1-kontext-dev_ComfyUI/resolve/main/split_files/diffusion_models/flux1-dev-kontext_fp8_scaled.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/flux1-kontext-dev_ComfyUI"),
            Author = "Black Forest Labs",
            LicenseType = "FLUX.1 [dev] Non-Commercial",
            LicenseUrl = new Uri("https://huggingface.co/black-forest-labs/FLUX.1-dev/blob/main/LICENSE.md"),
            ContextType = SharedFolderType.DiffusionModels,
        },
        FluxVae,
        ClipLTextEncoder,
        T5XxlFp8ScaledTextEncoder,
    ];

    #endregion

    #region Qwen Image Edit Models

    /// <summary>
    /// All required models for Qwen Image Edit
    /// </summary>
    public static IReadOnlyList<RemoteResource> QwenImageEditModels { get; } =
    [
        // Qwen Image Edit UNET model (fp8 quantized, 2511 build)
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/Qwen-Image-Edit_ComfyUI/resolve/main/split_files/diffusion_models/qwen_image_edit_2511_fp8mixed.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/Qwen-Image-Edit_ComfyUI"),
            Author = "Alibaba Qwen",
            LicenseType = "Qwen License",
            ContextType = SharedFolderType.DiffusionModels,
        },
        QwenImageVae,
        Qwen25Vl7bTextEncoder,
    ];

    #endregion

    #region Flux.2 Klein Models

    /// <summary>
    /// All required models for Flux.2 Klein 4B image editing.
    /// Klein 4B is Apache 2.0 licensed (commercial use free). Klein 9B users can manually
    /// download the 9B UNET + qwen_3_8b text encoder into the same folders — the model
    /// manager substring-matches loosely enough to pick up either variant.
    /// </summary>
    public static IReadOnlyList<RemoteResource> Flux2KleinModels { get; } =
    [
        // Flux.2 Klein 4B UNET (distilled, ~7.75 GB, Apache 2.0)
        new()
        {
            Url = new Uri(
                "https://huggingface.co/Comfy-Org/flux2-klein-4B/resolve/main/split_files/diffusion_models/flux-2-klein-4b.safetensors"
            ),
            InfoUrl = new Uri("https://huggingface.co/Comfy-Org/flux2-klein-4B"),
            Author = "Black Forest Labs",
            LicenseType = "Apache 2.0",
            LicenseUrl = new Uri(
                "https://huggingface.co/black-forest-labs/FLUX.2-klein-4B/blob/main/LICENSE.md"
            ),
            ContextType = SharedFolderType.DiffusionModels,
        },
        // Flux.2 VAE (~336 MB, shared between all Flux.2 variants) + Qwen3 4B text
        // encoder (~8 GB, paired with Klein 4B)
        Flux2Vae,
        Qwen3_4bTextEncoder,
    ];

    #endregion

    #region Flux.2 Klein 9B Models

    /// <summary>
    /// Freely downloadable models for Flux.2 Klein 9B image editing.
    /// The 9B UNET itself is gated under the FLUX.2 license and can't be redistributed for
    /// one-click download, so 9B users supply that file manually. This list covers the
    /// pieces that <em>are</em> downloadable (the qwen_3_8b text encoder + shared Flux.2 VAE)
    /// so a 9B install missing only its encoder is offered the matching 8B encoder rather
    /// than the 4B one from <see cref="Flux2KleinModels"/>.
    /// </summary>
    public static IReadOnlyList<RemoteResource> Flux2Klein9BModels { get; } =
    [
        // Flux.2 VAE (~336 MB, shared between all Flux.2 variants) + Qwen3 8B text
        // encoder (~16.4 GB, paired with Klein 9B)
        Flux2Vae,
        Qwen3_8bTextEncoder,
    ];

    #endregion
}
