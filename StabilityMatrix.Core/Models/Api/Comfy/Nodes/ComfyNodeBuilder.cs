using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using OneOf;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Core.Models.Api.Comfy.Nodes;

/// <summary>
/// Builder functions for comfy nodes
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[Localizable(false)]
public class ComfyNodeBuilder
{
    public NodeDictionary Nodes { get; } = new();

    private static string GetRandomPrefix() => Guid.NewGuid().ToString()[..8];

    private const int MaxResolution = 16384;

    private string GetUniqueName(string nameBase)
    {
        var name = $"{nameBase}_1";
        for (var i = 0; Nodes.ContainsKey(name); i++)
        {
            if (i > 1_000_000)
            {
                throw new InvalidOperationException($"Could not find unique name for base {nameBase}");
            }

            name = $"{nameBase}_{i + 1}";
        }

        return name;
    }

    public record VAEEncode : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ImageNodeConnection Pixels { get; init; }
        public required VAENodeConnection Vae { get; init; }
    }

    public record VAEEncodeForInpaint : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ImageNodeConnection Pixels { get; init; }
        public required VAENodeConnection Vae { get; init; }
        public required ImageMaskConnection Mask { get; init; }

        [Range(0, 64)]
        public int GrowMaskBy { get; init; } = 6;
    }

    public record VAEDecode : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }
        public required VAENodeConnection Vae { get; init; }
    }

    public record KSampler : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required ulong Seed { get; init; }
        public required int Steps { get; init; }
        public required double Cfg { get; init; }
        public required string SamplerName { get; init; }
        public required string Scheduler { get; init; }
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
        public required double Denoise { get; init; }
    }

    public record KSamplerAdvanced : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [BoolStringMember("enable", "disable")]
        public required bool AddNoise { get; init; }
        public required ulong NoiseSeed { get; init; }
        public required int Steps { get; init; }
        public required double Cfg { get; init; }
        public required string SamplerName { get; init; }
        public required string Scheduler { get; init; }
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
        public required int StartAtStep { get; init; }
        public required int EndAtStep { get; init; }

        [BoolStringMember("enable", "disable")]
        public bool ReturnWithLeftoverNoise { get; init; }
    }

    public record SamplerCustom : ComfyTypedNodeBase<LatentNodeConnection, LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required bool AddNoise { get; init; }
        public required ulong NoiseSeed { get; init; }

        [Range(0d, 100d)]
        public required double Cfg { get; init; }

        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required SamplerNodeConnection Sampler { get; init; }
        public required SigmasNodeConnection Sigmas { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
    }

    public record KSamplerSelect : ComfyTypedNodeBase<SamplerNodeConnection>
    {
        public required string SamplerName { get; init; }
    }

    public record SDTurboScheduler : ComfyTypedNodeBase<SigmasNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [Range(1, 10)]
        public required int Steps { get; init; }

        [Range(0, 1.0)]
        public required double Denoise { get; init; }
    }

    public record EmptyLatentImage : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required int BatchSize { get; init; }
        public required int Height { get; init; }
        public required int Width { get; init; }
    }

    public record EmptyHunyuanLatentVideo : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int Length { get; init; }
        public required int BatchSize { get; init; }
    }

    public record CLIPSetLastLayer : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required ClipNodeConnection Clip { get; init; }

        [Range(-24, -1)]
        public int StopAtClipLayer { get; init; } = -1;
    }

    public record LatentFromBatch : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }

        [Range(0, 63)]
        public int BatchIndex { get; init; } = 0;

        [Range(1, 64)]
        public int Length { get; init; } = 1;
    }

    public record RepeatLatentBatch : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }

        [Range(1, 64)]
        public int Amount { get; init; } = 1;
    }

    public record LatentBlend : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required LatentNodeConnection Samples1 { get; init; }

        public required LatentNodeConnection Samples2 { get; init; }

        [Range(0d, 1d)]
        public double BlendFactor { get; init; } = 0.5;
    }

    public record ModelMergeSimple : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model1 { get; init; }

        public required ModelNodeConnection Model2 { get; init; }

        [Range(0d, 1d)]
        public double Ratio { get; init; } = 1;
    }

    public static NamedComfyNode<ImageNodeConnection> ImageUpscaleWithModel(
        string name,
        UpscaleModelNodeConnection upscaleModel,
        ImageNodeConnection image
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "ImageUpscaleWithModel",
            Inputs = new Dictionary<string, object?>
            {
                ["upscale_model"] = upscaleModel.Data,
                ["image"] = image.Data,
            },
        };
    }

    public static NamedComfyNode<UpscaleModelNodeConnection> UpscaleModelLoader(string name, string modelName)
    {
        return new NamedComfyNode<UpscaleModelNodeConnection>(name)
        {
            ClassType = "UpscaleModelLoader",
            Inputs = new Dictionary<string, object?> { ["model_name"] = modelName },
        };
    }

    public static NamedComfyNode<ImageNodeConnection> ImageScale(
        string name,
        ImageNodeConnection image,
        string method,
        int height,
        int width,
        bool crop
    )
    {
        return new NamedComfyNode<ImageNodeConnection>(name)
        {
            ClassType = "ImageScale",
            Inputs = new Dictionary<string, object?>
            {
                ["image"] = image.Data,
                ["upscale_method"] = method,
                ["height"] = height,
                ["width"] = width,
                ["crop"] = crop ? "center" : "disabled",
            },
        };
    }

    public record VAELoader : ComfyTypedNodeBase<VAENodeConnection>
    {
        public required string VaeName { get; init; }
    }

    public static NamedComfyNode<ModelNodeConnection, ClipNodeConnection> LoraLoader(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        string loraName,
        double strengthModel,
        double strengthClip
    )
    {
        return new NamedComfyNode<ModelNodeConnection, ClipNodeConnection>(name)
        {
            ClassType = "LoraLoader",
            Inputs = new Dictionary<string, object?>
            {
                ["model"] = model.Data,
                ["clip"] = clip.Data,
                ["lora_name"] = loraName,
                ["strength_model"] = strengthModel,
                ["strength_clip"] = strengthClip,
            },
        };
    }

    public record CheckpointLoader
        : ComfyTypedNodeBase<ModelNodeConnection, ClipNodeConnection, VAENodeConnection>
    {
        public required string ConfigName { get; init; }
        public required string CkptName { get; init; }
    }

    public record CheckpointLoaderSimple
        : ComfyTypedNodeBase<ModelNodeConnection, ClipNodeConnection, VAENodeConnection>
    {
        public required string CkptName { get; init; }
    }

    public record ImageOnlyCheckpointLoader
        : ComfyTypedNodeBase<ModelNodeConnection, ClipVisionNodeConnection, VAENodeConnection>
    {
        public required string CkptName { get; init; }
    }

    public record FreeU : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required double B1 { get; init; }
        public required double B2 { get; init; }
        public required double S1 { get; init; }
        public required double S2 { get; init; }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public record CLIPTextEncode : ComfyTypedNodeBase<ConditioningNodeConnection>
    {
        public required ClipNodeConnection Clip { get; init; }
        public required OneOf<string, StringNodeConnection> Text { get; init; }
    }

    public record LoadImage : ComfyTypedNodeBase<ImageNodeConnection, ImageMaskConnection>
    {
        /// <summary>
        /// Path relative to the Comfy input directory
        /// </summary>
        public required string Image { get; init; }
    }

    public record LoadImageMask : ComfyTypedNodeBase<ImageMaskConnection>
    {
        /// <summary>
        /// Path relative to the Comfy input directory
        /// </summary>
        public required string Image { get; init; }

        /// <summary>
        /// Color channel to use as mask.
        /// ("alpha", "red", "green", "blue")
        /// </summary>
        public string Channel { get; init; } = "alpha";
    }

    public record PreviewImage : ComfyTypedNodeBase
    {
        public required ImageNodeConnection Images { get; init; }
    }

    public record ImageSharpen : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required ImageNodeConnection Image { get; init; }
        public required int SharpenRadius { get; init; }
        public required double Sigma { get; init; }
        public required double Alpha { get; init; }
    }

    public record ControlNetLoader : ComfyTypedNodeBase<ControlNetNodeConnection>
    {
        public required string ControlNetName { get; init; }
    }

    public record ControlNetApplyAdvanced
        : ComfyTypedNodeBase<ConditioningNodeConnection, ConditioningNodeConnection>
    {
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required ControlNetNodeConnection ControlNet { get; init; }
        public required ImageNodeConnection Image { get; init; }
        public required double Strength { get; init; }
        public required double StartPercent { get; init; }
        public required double EndPercent { get; init; }
    }

    public record SVD_img2vid_Conditioning
        : ComfyTypedNodeBase<ConditioningNodeConnection, ConditioningNodeConnection, LatentNodeConnection>
    {
        public required ClipVisionNodeConnection ClipVision { get; init; }
        public required ImageNodeConnection InitImage { get; init; }
        public required VAENodeConnection Vae { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int VideoFrames { get; init; }
        public required int MotionBucketId { get; init; }
        public required int Fps { get; set; }
        public required double AugmentationLevel { get; init; }
    }

    public record VideoLinearCFGGuidance : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required double MinCfg { get; init; }
    }

    public record SaveAnimatedWEBP : ComfyTypedNodeBase
    {
        public required ImageNodeConnection Images { get; init; }
        public required string FilenamePrefix { get; init; }
        public required double Fps { get; init; }
        public required bool Lossless { get; init; }
        public required int Quality { get; init; }
        public required string Method { get; init; }
    }

    public record UNETLoader : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required string UnetName { get; init; }

        /// <summary>
        /// possible values: "default", "fp8_e4m3fn", "fp8_e5m2"
        /// </summary>
        public required string WeightDtype { get; init; }
    }

    public record CLIPLoader : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required string ClipName { get; init; }

        /// <summary>
        /// possible values: "stable_diffusion", "stable_cascade", "sd3", "stable_audio", "mochi"
        /// </summary>
        public required string Type { get; init; }
    }

    public record DualCLIPLoader : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required string ClipName1 { get; init; }
        public required string ClipName2 { get; init; }

        /// <summary>
        /// possible values: "sdxl", "sd3", "flux"
        /// </summary>
        public required string Type { get; init; }
    }

    public record TripleCLIPLoader : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required string ClipName1 { get; init; }
        public required string ClipName2 { get; init; }
        public required string ClipName3 { get; init; }

        // no type, always sd3 I guess?
    }

    public record QuadrupleCLIPLoader : ComfyTypedNodeBase<ClipNodeConnection>
    {
        public required string ClipName1 { get; init; }
        public required string ClipName2 { get; init; }
        public required string ClipName3 { get; init; }
        public required string ClipName4 { get; init; }

        // no type, always HiDream I guess?
    }

    public record CLIPVisionLoader : ComfyTypedNodeBase<ClipVisionNodeConnection>
    {
        public required string ClipName { get; init; }
    }

    public record CLIPVisionEncode : ComfyTypedNodeBase<ClipVisionOutputNodeConnection>
    {
        public required ClipVisionNodeConnection ClipVision { get; init; }
        public required ImageNodeConnection Image { get; init; }
        public required string Crop { get; set; }
    }

    public record FluxGuidance : ComfyTypedNodeBase<ConditioningNodeConnection>
    {
        public required ConditioningNodeConnection Conditioning { get; init; }

        [Range(0.0d, 100.0d)]
        public required double Guidance { get; init; }
    }

    public record BasicGuider : ComfyTypedNodeBase<GuiderNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required ConditioningNodeConnection Conditioning { get; init; }
    }

    public record EmptySD3LatentImage : ComfyTypedNodeBase<LatentNodeConnection>
    {
        [Range(16, MaxResolution)]
        public int Width { get; init; } = 1024;

        [Range(16, MaxResolution)]
        public int Height { get; init; } = 1024;

        [Range(1, 4096)]
        public int BatchSize { get; init; } = 1;
    }

    public record RandomNoise : ComfyTypedNodeBase<NoiseNodeConnection>
    {
        [Range(0, int.MaxValue)]
        public ulong NoiseSeed { get; init; }
    }

    public record BasicScheduler : ComfyTypedNodeBase<SigmasNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required string Scheduler { get; init; }

        [Range(1, 10000)]
        public int Steps { get; init; } = 20;

        [Range(0.0d, 1.0d)]
        public double Denoise { get; init; } = 1.0;
    }

    public record SamplerCustomAdvanced : ComfyTypedNodeBase<LatentNodeConnection, LatentNodeConnection>
    {
        public required NoiseNodeConnection Noise { get; init; }
        public required GuiderNodeConnection Guider { get; init; }
        public required SamplerNodeConnection Sampler { get; init; }
        public required SigmasNodeConnection Sigmas { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
    }

    public record ModelSamplingDiscrete : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        /// <summary>
        /// Options: "eps", "v_prediction", "lcm", "x0"
        /// </summary>
        public required string Sampling { get; set; }
        public required bool Zsnr { get; init; }
    }

    public record ModelSamplingSD3 : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [Range(0, 100)]
        public required double Shift { get; init; }
    }

    public record RescaleCFG : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }
        public required double Multiplier { get; init; }
    }

    public record SetLatentNoiseMask : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }
        public required ImageMaskConnection Mask { get; init; }
    }

    public record AlignYourStepsScheduler : ComfyTypedNodeBase<SigmasNodeConnection>
    {
        /// <summary>
        /// options: SD1, SDXL, SVD
        /// </summary>
        public required string ModelType { get; init; }

        [Range(1, 10000)]
        public required int Steps { get; init; }

        [Range(0.0d, 1.0d)]
        public required double Denoise { get; init; }
    }

    public record CFGGuider : ComfyTypedNodeBase<GuiderNodeConnection>
    {
        public required ModelNodeConnection Model { get; set; }
        public required ConditioningNodeConnection Positive { get; set; }
        public required ConditioningNodeConnection Negative { get; set; }

        [Range(0.0d, 100.0d)]
        public required double Cfg { get; set; }
    }

    /// <summary>
    /// outputs: positive, negative, latent
    /// </summary>
    public record WanImageToVideo
        : ComfyTypedNodeBase<ConditioningNodeConnection, ConditioningNodeConnection, LatentNodeConnection>
    {
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required VAENodeConnection Vae { get; init; }
        public required ClipVisionOutputNodeConnection ClipVisionOutput { get; init; }
        public required ImageNodeConnection StartImage { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required int Length { get; init; }
        public required int BatchSize { get; init; }
    }

    [TypedNodeOptions(
        Name = "CheckpointLoaderNF4",
        RequiredExtensions = ["https://github.com/comfyanonymous/ComfyUI_bitsandbytes_NF4"]
    )]
    public record CheckpointLoaderNF4
        : ComfyTypedNodeBase<ModelNodeConnection, ClipNodeConnection, VAENodeConnection>
    {
        public required string CkptName { get; init; }
    }

    [TypedNodeOptions(
        Name = "UnetLoaderGGUF",
        RequiredExtensions = ["https://github.com/city96/ComfyUI-GGUF"]
    )]
    public record UnetLoaderGGUF : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required string UnetName { get; init; }
    }

    [TypedNodeOptions(
        Name = "Inference_Core_PromptExpansion",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.2.0"]
    )]
    public record PromptExpansion : ComfyTypedNodeBase<StringNodeConnection>
    {
        public required string ModelName { get; init; }
        public required OneOf<string, StringNodeConnection> Text { get; init; }
        public required ulong Seed { get; init; }
        public bool LogPrompt { get; init; }
    }

    [TypedNodeOptions(
        Name = "Inference_Core_AIO_Preprocessor",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.2.0"]
    )]
    public record AIOPreprocessor : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required ImageNodeConnection Image { get; init; }

        public required string Preprocessor { get; init; }

        [Range(64, 16384)]
        public int Resolution { get; init; } = 512;
    }

    [TypedNodeOptions(
        Name = "Inference_Core_ReferenceOnlySimple",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.3.0"]
    )]
    public record ReferenceOnlySimple : ComfyTypedNodeBase<ModelNodeConnection, LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        public required LatentNodeConnection Reference { get; init; }

        [Range(1, 64)]
        public int BatchSize { get; init; } = 1;
    }

    [TypedNodeOptions(
        Name = "Inference_Core_LayeredDiffusionApply",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.4.0"]
    )]
    public record LayeredDiffusionApply : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        /// <summary>
        /// Available configs:
        /// <para>SD15, Attention Injection, attn_sharing</para>
        /// <para>SDXL, Conv Injection</para>
        /// <para>SDXL, Attention Injection</para>
        /// </summary>
        public required string Config { get; init; }

        [Range(-1d, 3d)]
        public double Weight { get; init; } = 1.0;
    }

    [TypedNodeOptions(
        Name = "Inference_Core_LayeredDiffusionDecodeRGBA",
        RequiredExtensions = ["https://github.com/LykosAI/ComfyUI-Inference-Core-Nodes >= 0.4.0"]
    )]
    public record LayeredDiffusionDecodeRgba : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required LatentNodeConnection Samples { get; init; }

        public required ImageNodeConnection Images { get; init; }

        /// <summary>
        /// Either "SD15" or "SDXL"
        /// </summary>
        public required string SdVersion { get; init; }

        [Range(1, 4096)]
        public int SubBatchSize { get; init; } = 16;
    }

    [TypedNodeOptions(
        Name = "UltralyticsDetectorProvider",
        RequiredExtensions = [
            "https://github.com/ltdrdata/ComfyUI-Impact-Pack",
            "https://github.com/ltdrdata/ComfyUI-Impact-Subpack",
        ]
    )]
    public record UltralyticsDetectorProvider
        : ComfyTypedNodeBase<BboxDetectorNodeConnection, SegmDetectorNodeConnection>
    {
        public required string ModelName { get; init; }
    }

    [TypedNodeOptions(
        Name = "SAMLoader",
        RequiredExtensions = [
            "https://github.com/ltdrdata/ComfyUI-Impact-Pack",
            "https://github.com/ltdrdata/ComfyUI-Impact-Subpack",
        ]
    )]
    public record SamLoader : ComfyTypedNodeBase<SamModelNodeConnection>
    {
        public required string ModelName { get; init; }

        /// <summary>
        /// options: AUTO, Prefer GPU, CPU
        /// </summary>
        public required string DeviceMode { get; init; }
    }

    [TypedNodeOptions(
        Name = "FaceDetailer",
        RequiredExtensions = [
            "https://github.com/ltdrdata/ComfyUI-Impact-Pack",
            "https://github.com/ltdrdata/ComfyUI-Impact-Subpack",
        ]
    )]
    public record FaceDetailer : ComfyTypedNodeBase<ImageNodeConnection>
    {
        public required ImageNodeConnection Image { get; init; }
        public required ModelNodeConnection Model { get; init; }
        public required ClipNodeConnection Clip { get; init; }
        public required VAENodeConnection Vae { get; init; }
        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required BboxDetectorNodeConnection BboxDetector { get; init; }
        public required double GuideSize { get; init; } = 512.0;

        /// <summary>
        /// true: 'bbox'
        /// false: 'crop_region'
        /// </summary>
        public required bool GuideSizeFor { get; init; } = true;
        public required double MaxSize { get; init; } = 1024.0;
        public required ulong Seed { get; init; }
        public required int Steps { get; init; } = 20;
        public required double Cfg { get; init; } = 8.0d;
        public required string SamplerName { get; init; }
        public required string Scheduler { get; init; }
        public required double Denoise { get; init; } = 0.5d;
        public required int Feather { get; init; } = 5;
        public required bool NoiseMask { get; init; } = true;
        public required bool ForceInpaint { get; init; } = true;

        [Range(0.0, 1.0)]
        public required double BboxThreshold { get; init; } = 0.5d;

        [Range(-512, 512)]
        public required int BboxDilation { get; init; } = 10;

        [Range(1.0, 10.0)]
        public required double BboxCropFactor { get; init; } = 3.0d;

        /// <summary>
        /// options: ["center-1", "horizontal-2", "vertical-2", "rect-4", "diamond-4", "mask-area", "mask-points", "mask-point-bbox", "none"]
        /// </summary>
        public required string SamDetectionHint { get; init; }

        [Range(-512, 512)]
        public required int SamDilation { get; init; }

        [Range(0.0d, 1.0d)]
        public required double SamThreshold { get; init; } = 0.93d;

        [Range(0, 1000)]
        public required int SamBboxExpansion { get; init; }

        [Range(0.0d, 1.0d)]
        public required double SamMaskHintThreshold { get; init; } = 0.7d;

        /// <summary>
        /// options: ["False", "Small", "Outter"]
        /// </summary>
        public required string SamMaskHintUseNegative { get; init; } = "False";

        public required string Wildcard { get; init; }

        [Range(1, 32768)]
        public required int DropSize { get; init; } = 10;

        [Range(1, 10)]
        public required int Cycle { get; init; } = 1;

        public SamModelNodeConnection? SamModelOpt { get; set; }
        public SegmDetectorNodeConnection? SegmDetectorOpt { get; set; }
        public bool TiledEncode { get; init; }
        public bool TiledDecode { get; init; }
    }

    /// <summary>
    /// Plasma Noise generation node (Lykos_JDC_Plasma)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_Plasma",
        RequiredExtensions = ["https://github.com/LykosAI/inference-comfy-plasma"]
    )] // Name corrected, Extensions added
    public record PlasmaNoise : ComfyTypedNodeBase<ImageNodeConnection>
    {
        [Range(128, 8192)]
        public required int Width { get; init; } = 512;

        [Range(128, 8192)]
        public required int Height { get; init; } = 512;

        [Range(0.5d, 32.0d)]
        public required double Turbulence { get; init; } = 2.75;

        [Range(-1, 255)]
        public required int ValueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int ValueMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMax { get; init; } = -1;

        [Range(0UL, ulong.MaxValue)] // Match Python's max int size
        public required ulong Seed { get; init; } = 0;
    }

    /// <summary>
    /// Random Noise generation node (Lykos_JDC_RandNoise)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_RandNoise",
        RequiredExtensions = ["https://github.com/LykosAI/inference-comfy-plasma"]
    )] // Name corrected, Extensions added
    public record RandNoise : ComfyTypedNodeBase<ImageNodeConnection>
    {
        [Range(128, 8192)]
        public required int Width { get; init; } = 512;

        [Range(128, 8192)]
        public required int Height { get; init; } = 512;

        [Range(-1, 255)]
        public required int ValueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int ValueMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMax { get; init; } = -1;

        [Range(0UL, ulong.MaxValue)]
        public required ulong Seed { get; init; } = 0;
    }

    /// <summary>
    /// Greyscale Noise generation node (Lykos_JDC_GreyNoise)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_GreyNoise",
        RequiredExtensions = ["https://github.com/LykosAI/inference-comfy-plasma"]
    )] // Name corrected, Extensions added
    public record GreyNoise : ComfyTypedNodeBase<ImageNodeConnection>
    {
        [Range(128, 8192)]
        public required int Width { get; init; } = 512;

        [Range(128, 8192)]
        public required int Height { get; init; } = 512;

        [Range(-1, 255)]
        public required int ValueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int ValueMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMax { get; init; } = -1;

        [Range(0UL, ulong.MaxValue)]
        public required ulong Seed { get; init; } = 0;
    }

    /// <summary>
    /// Pink Noise generation node (Lykos_JDC_PinkNoise)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_PinkNoise",
        RequiredExtensions = ["https://github.com/LykosAI/inference-comfy-plasma"]
    )] // Name corrected, Extensions added
    public record PinkNoise : ComfyTypedNodeBase<ImageNodeConnection>
    {
        [Range(128, 8192)]
        public required int Width { get; init; } = 512;

        [Range(128, 8192)]
        public required int Height { get; init; } = 512;

        [Range(-1, 255)]
        public required int ValueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int ValueMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMax { get; init; } = -1;

        [Range(0UL, ulong.MaxValue)]
        public required ulong Seed { get; init; } = 0;
    }

    /// <summary>
    /// Brown Noise generation node (Lykos_JDC_BrownNoise)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_BrownNoise",
        RequiredExtensions = new[] { "https://github.com/LykosAI/inference-comfy-plasma" }
    )] // Name corrected, Extensions added
    public record BrownNoise : ComfyTypedNodeBase<ImageNodeConnection>
    {
        [Range(128, 8192)]
        public required int Width { get; init; } = 512;

        [Range(128, 8192)]
        public required int Height { get; init; } = 512;

        [Range(-1, 255)]
        public required int ValueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int ValueMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int RedMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int GreenMax { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMin { get; init; } = -1;

        [Range(-1, 255)]
        public required int BlueMax { get; init; } = -1;

        [Range(0UL, ulong.MaxValue)]
        public required ulong Seed { get; init; } = 0;
    }

    /// <summary>
    /// CUDNN Toggle node for controlling CUDA Deep Neural Network library settings (CUDNNToggleAutoPassthrough)
    /// </summary>
    [TypedNodeOptions(Name = "CUDNNToggleAutoPassthrough")]
    public record CUDNNToggleAutoPassthrough
        : ComfyTypedNodeBase<ModelNodeConnection, ConditioningNodeConnection, LatentNodeConnection>
    {
        public ModelNodeConnection? Model { get; init; }
        public ConditioningNodeConnection? Conditioning { get; init; }
        public LatentNodeConnection? Latent { get; init; }
        public required bool enable_cudnn { get; init; } = false;
        public required bool cudnn_benchmark { get; init; } = false;
    }

    /// <summary>
    /// Custom KSampler node using alternative noise distribution (Lykos_JDC_PlasmaSampler)
    /// </summary>
    [TypedNodeOptions(
        Name = "Lykos_JDC_PlasmaSampler",
        RequiredExtensions = ["https://github.com/LykosAI/inference-comfy-plasma"]
    )] // Name corrected, Extensions added
    public record PlasmaSampler : ComfyTypedNodeBase<LatentNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [Range(0UL, ulong.MaxValue)]
        public required ulong NoiseSeed { get; init; } = 0;

        [Range(1, 10000)]
        public required int Steps { get; init; } = 20;

        [Range(0.0d, 100.0d)]
        public required double Cfg { get; init; } = 7.0;

        [Range(0.0d, 1.0d)]
        public required double Denoise { get; init; } = 0.9; // Default from Python code

        [Range(0.0d, 1.0d)]
        public required double LatentNoise { get; init; } = 0.05; // Default from Python code

        /// <summary>
        /// Noise distribution type. Expected values: "default", "rand".
        /// Validation should ensure one of these values is passed.
        /// </summary>
        public required string DistributionType { get; init; } = "rand";

        /// <summary>
        /// Name of the KSampler sampler (e.g., "euler", "dpmpp_2m_sde").
        /// Should correspond to available samplers in comfy.samplers.KSampler.SAMPLERS.
        /// </summary>
        public required string SamplerName { get; init; } // No default in Python, must be provided

        /// <summary>
        /// Name of the KSampler scheduler (e.g., "normal", "karras", "sgm_uniform").
        /// Should correspond to available schedulers in comfy.samplers.KSampler.SCHEDULERS.
        /// </summary>
        public required string Scheduler { get; init; } // No default in Python, must be provided

        public required ConditioningNodeConnection Positive { get; init; }
        public required ConditioningNodeConnection Negative { get; init; }
        public required LatentNodeConnection LatentImage { get; init; }
    }

    [TypedNodeOptions(
        Name = "NRS",
        RequiredExtensions = ["https://github.com/Reithan/negative_rejection_steering"]
    )]
    public record NRS : ComfyTypedNodeBase<ModelNodeConnection>
    {
        public required ModelNodeConnection Model { get; init; }

        [Range(-30.0f, 30.0f)]
        public required double Skew { get; set; }

        [Range(-30.0f, 30.0f)]
        public required double Stretch { get; set; }

        [Range(0f, 1f)]
        public required double Squash { get; set; }
    }

    public ImageNodeConnection Lambda_LatentToImage(LatentNodeConnection latent, VAENodeConnection vae)
    {
        var name = GetUniqueName("VAEDecode");
        return Nodes
            .AddTypedNode(
                new VAEDecode
                {
                    Name = name,
                    Samples = latent,
                    Vae = vae,
                }
            )
            .Output;
    }

    public LatentNodeConnection Lambda_ImageToLatent(ImageNodeConnection pixels, VAENodeConnection vae)
    {
        var name = GetUniqueName("VAEEncode");
        return Nodes
            .AddTypedNode(
                new VAEEncode
                {
                    Name = name,
                    Pixels = pixels,
                    Vae = vae,
                }
            )
            .Output;
    }

    /// <summary>
    /// Create a group node that upscales a given image with a given model
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleWithModel(
        string name,
        string modelName,
        ImageNodeConnection image
    )
    {
        var modelLoader = Nodes.AddNamedNode(UpscaleModelLoader($"{name}_UpscaleModelLoader", modelName));

        var upscaler = Nodes.AddNamedNode(
            ImageUpscaleWithModel($"{name}_ImageUpscaleWithModel", modelLoader.Output, image)
        );

        return upscaler;
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public PrimaryNodeConnection Group_Upscale(
        string name,
        PrimaryNodeConnection primary,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return primary.Match<PrimaryNodeConnection>(
                latent =>
                    Nodes
                        .AddNamedNode(
                            new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                            {
                                ClassType = "LatentUpscale",
                                Inputs = new Dictionary<string, object?>
                                {
                                    ["upscale_method"] = upscaleInfo.Name,
                                    ["width"] = width,
                                    ["height"] = height,
                                    ["crop"] = "disabled",
                                    ["samples"] = latent.Data,
                                },
                            }
                        )
                        .Output,
                image =>
                    Nodes
                        .AddNamedNode(
                            ImageScale($"{name}_ImageUpscale", image, upscaleInfo.Name, height, width, false)
                        )
                        .Output
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space if needed
            var samplerImage = GetPrimaryAsImage(primary, vae);

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            return resizedScaled.Output;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to a given size
    /// </summary>
    public NamedComfyNode<LatentNodeConnection> Group_UpscaleToLatent(
        string name,
        LatentNodeConnection latent,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return Nodes.AddNamedNode(
                new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "LatentUpscale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                        ["samples"] = latent.Data,
                    },
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latent,
                    Vae = vae,
                }
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // Convert back to latent space
            return Nodes.AddTypedNode(
                new VAEEncode
                {
                    Name = $"{name}_VAEEncode",
                    Pixels = resizedScaled.Output,
                    Vae = vae,
                }
            );
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_LatentUpscaleToImage(
        string name,
        LatentNodeConnection latent,
        VAENodeConnection vae,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            var latentUpscale = Nodes.AddNamedNode(
                new NamedComfyNode<LatentNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "LatentUpscale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                        ["samples"] = latent.Data,
                    },
                }
            );

            // Convert to image space
            return Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latentUpscale.Output,
                    Vae = vae,
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Convert to image space
            var samplerImage = Nodes.AddTypedNode(
                new VAEDecode
                {
                    Name = $"{name}_VAEDecode",
                    Samples = latent,
                    Vae = vae,
                }
            );

            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel(
                $"{name}_ModelUpscale",
                upscaleInfo.Name,
                samplerImage.Output
            );

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // No need to convert back to latent space
            return resizedScaled;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that scales a given image to image output
    /// </summary>
    public NamedComfyNode<ImageNodeConnection> Group_UpscaleToImage(
        string name,
        ImageNodeConnection image,
        ComfyUpscaler upscaleInfo,
        int width,
        int height
    )
    {
        if (upscaleInfo.Type == ComfyUpscalerType.Latent)
        {
            return Nodes.AddNamedNode(
                new NamedComfyNode<ImageNodeConnection>($"{name}_LatentUpscale")
                {
                    ClassType = "ImageScale",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["image"] = image,
                        ["upscale_method"] = upscaleInfo.Name,
                        ["width"] = width,
                        ["height"] = height,
                        ["crop"] = "disabled",
                    },
                }
            );
        }

        if (upscaleInfo.Type == ComfyUpscalerType.ESRGAN)
        {
            // Do group upscale
            var modelUpscaler = Group_UpscaleWithModel($"{name}_ModelUpscale", upscaleInfo.Name, image);

            // Since the model upscale is fixed to model (2x/4x), scale it again to the requested size
            var resizedScaled = Nodes.AddNamedNode(
                ImageScale($"{name}_ImageScale", modelUpscaler.Output, "bilinear", height, width, false)
            );

            // No need to convert back to latent space
            return resizedScaled;
        }

        throw new InvalidOperationException($"Unknown upscaler type: {upscaleInfo.Type}");
    }

    /// <summary>
    /// Create a group node that loads multiple Lora's in series
    /// </summary>
    public NamedComfyNode<ModelNodeConnection, ClipNodeConnection> Group_LoraLoadMany(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        IEnumerable<(string FileName, double? ModelWeight, double? ClipWeight)> loras
    )
    {
        NamedComfyNode<ModelNodeConnection, ClipNodeConnection>? currentNode = null;

        foreach (var (i, loraNetwork) in loras.Enumerate())
        {
            currentNode = Nodes.AddNamedNode(
                LoraLoader(
                    $"{name}_LoraLoader_{i + 1}",
                    model,
                    clip,
                    loraNetwork.FileName,
                    loraNetwork.ModelWeight ?? 1,
                    loraNetwork.ClipWeight ?? 1
                )
            );

            // Connect to previous node
            model = currentNode.Output1;
            clip = currentNode.Output2;
        }

        return currentNode ?? throw new InvalidOperationException("No lora networks given");
    }

    /// <summary>
    /// Create a group node that loads multiple Lora's in series
    /// </summary>
    public NamedComfyNode<ModelNodeConnection, ClipNodeConnection> Group_LoraLoadMany(
        string name,
        ModelNodeConnection model,
        ClipNodeConnection clip,
        IEnumerable<(LocalModelFile ModelFile, double? ModelWeight, double? ClipWeight)> loras
    )
    {
        NamedComfyNode<ModelNodeConnection, ClipNodeConnection>? currentNode = null;

        foreach (var (i, loraNetwork) in loras.Enumerate())
        {
            currentNode = Nodes.AddNamedNode(
                LoraLoader(
                    $"{name}_LoraLoader_{i + 1}",
                    model,
                    clip,
                    loraNetwork.ModelFile.RelativePathFromSharedFolder,
                    loraNetwork.ModelWeight ?? 1,
                    loraNetwork.ClipWeight ?? 1
                )
            );

            // Connect to previous node
            model = currentNode.Output1;
            clip = currentNode.Output2;
        }

        return currentNode ?? throw new InvalidOperationException("No lora networks given");
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent()
    {
        if (Connections.Primary?.IsT0 == true)
        {
            return Connections.Primary.AsT0;
        }

        return GetPrimaryAsLatent(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            Connections.GetDefaultVAE()
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent(PrimaryNodeConnection primary, VAENodeConnection vae)
    {
        return primary.Match(latent => latent, image => Lambda_ImageToLatent(image, vae));
    }

    /// <summary>
    /// Get or convert latest primary connection to latent
    /// </summary>
    public LatentNodeConnection GetPrimaryAsLatent(VAENodeConnection vae)
    {
        if (Connections.Primary?.IsT0 == true)
        {
            return Connections.Primary.AsT0;
        }

        return GetPrimaryAsLatent(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            vae
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage()
    {
        if (Connections.Primary?.IsT1 == true)
        {
            return Connections.Primary.AsT1;
        }

        return GetPrimaryAsImage(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            Connections.GetDefaultVAE()
        );
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage(PrimaryNodeConnection primary, VAENodeConnection vae)
    {
        return primary.Match(latent => Lambda_LatentToImage(latent, vae), image => image);
    }

    /// <summary>
    /// Get or convert latest primary connection to image
    /// </summary>
    public ImageNodeConnection GetPrimaryAsImage(VAENodeConnection vae)
    {
        if (Connections.Primary?.IsT1 == true)
        {
            return Connections.Primary.AsT1;
        }

        return GetPrimaryAsImage(
            Connections.Primary ?? throw new NullReferenceException("No primary connection"),
            vae
        );
    }

    /// <summary>
    /// Convert to a NodeDictionary
    /// </summary>
    public NodeDictionary ToNodeDictionary()
    {
        Nodes.NormalizeConnectionTypes();
        return Nodes;
    }

    public class NodeBuilderConnections
    {
        public ulong Seed { get; set; }

        public int BatchSize { get; set; } = 1;
        public int? BatchIndex { get; set; }
        public int? PrimarySteps { get; set; }
        public double? PrimaryCfg { get; set; }
        public string? PrimaryModelType { get; set; }

        public OneOf<string, StringNodeConnection> PositivePrompt { get; set; }
        public OneOf<string, StringNodeConnection> NegativePrompt { get; set; }

        public ClipNodeConnection? BaseClip { get; set; }
        public ClipVisionNodeConnection? BaseClipVision { get; set; }

        public Dictionary<string, ModelConnections> Models { get; } =
            new() { ["Base"] = new ModelConnections("Base"), ["Refiner"] = new ModelConnections("Refiner") };

        /// <summary>
        /// ModelConnections from <see cref="Models"/> with <see cref="ModelConnections.Model"/> set
        /// </summary>
        public IEnumerable<ModelConnections> LoadedModels => Models.Values.Where(m => m.Model is not null);

        public ModelConnections Base => Models["Base"];
        public ModelConnections Refiner => Models["Refiner"];

        public Dictionary<string, ModuleApplyStepTemporaryArgs?> SamplerTemporaryArgs { get; } = new();

        public ModuleApplyStepTemporaryArgs? BaseSamplerTemporaryArgs
        {
            get => SamplerTemporaryArgs.GetValueOrDefault("Base");
            set => SamplerTemporaryArgs["Base"] = value;
        }

        /// <summary>
        /// The last primary set latent value, updated when <see cref="Primary"/> is set to a latent value.
        /// </summary>
        public LatentNodeConnection? LastPrimaryLatent { get; private set; }

        private PrimaryNodeConnection? primary;

        public PrimaryNodeConnection? Primary
        {
            get => primary;
            set
            {
                if (value?.IsT0 == true)
                {
                    LastPrimaryLatent = value.AsT0;
                }
                primary = value;
            }
        }

        public VAENodeConnection? PrimaryVAE { get; set; }
        public Size PrimarySize { get; set; }

        public ComfySampler? PrimarySampler { get; set; }
        public ComfyScheduler? PrimaryScheduler { get; set; }

        public GuiderNodeConnection PrimaryGuider { get; set; }
        public NoiseNodeConnection PrimaryNoise { get; set; }
        public SigmasNodeConnection PrimarySigmas { get; set; }
        public SamplerNodeConnection PrimarySamplerNode { get; set; }

        public List<NamedComfyNode> OutputNodes { get; } = new();

        public IEnumerable<string> OutputNodeNames => OutputNodes.Select(n => n.Name);

        public ModelNodeConnection GetRefinerOrBaseModel()
        {
            return Refiner.Model
                ?? Base.Model
                ?? throw new NullReferenceException("No Refiner or Base Model");
        }

        public ConditioningConnections GetRefinerOrBaseConditioning()
        {
            return Refiner.Conditioning
                ?? Base.Conditioning
                ?? throw new NullReferenceException("No Refiner or Base Conditioning");
        }

        public VAENodeConnection GetDefaultVAE()
        {
            return PrimaryVAE ?? Refiner.VAE ?? Base.VAE ?? throw new NullReferenceException("No VAE");
        }
    }

    public NodeBuilderConnections Connections { get; } = new();
}
