using System.Text.Json.Serialization;

namespace StabilityMatrix.Core.Models;

public class FooocusConfig
{
    [JsonPropertyName("path_checkpoints")]
    public string PathCheckpoints { get; set; }

    [JsonPropertyName("path_loras")]
    public string PathLoras { get; set; }

    [JsonPropertyName("path_embeddings")]
    public string PathEmbeddings { get; set; }

    [JsonPropertyName("path_vae_approx")]
    public string PathVaeApprox { get; set; }

    [JsonPropertyName("path_upscale_models")]
    public string PathUpscaleModels { get; set; }

    [JsonPropertyName("path_inpaint")]
    public string PathInpaint { get; set; }

    [JsonPropertyName("path_controlnet")]
    public string PathControlnet { get; set; }

    [JsonPropertyName("path_clip_vision")]
    public string PathClipVision { get; set; }

    [JsonPropertyName("path_fooocus_expansion")]
    public string PathFooocusExpansion { get; set; }

    [JsonPropertyName("path_outputs")]
    public string PathOutputs { get; set; }

    [JsonPropertyName("default_model")]
    public string DefaultModel { get; set; }

    [JsonPropertyName("default_refiner")]
    public string DefaultRefiner { get; set; }

    [JsonPropertyName("default_refiner_switch")]
    public double DefaultRefinerSwitch { get; set; }

    [JsonPropertyName("default_cfg_scale")]
    public long DefaultCfgScale { get; set; }

    [JsonPropertyName("default_sample_sharpness")]
    public long DefaultSampleSharpness { get; set; }

    [JsonPropertyName("default_sampler")]
    public string DefaultSampler { get; set; }

    [JsonPropertyName("default_scheduler")]
    public string DefaultScheduler { get; set; }

    [JsonPropertyName("default_styles")]
    public string[] DefaultStyles { get; set; }

    [JsonPropertyName("default_prompt_negative")]
    public string DefaultPromptNegative { get; set; }

    [JsonPropertyName("default_prompt")]
    public string DefaultPrompt { get; set; }

    [JsonPropertyName("default_performance")]
    public string DefaultPerformance { get; set; }

    [JsonPropertyName("default_advanced_checkbox")]
    public bool DefaultAdvancedCheckbox { get; set; }

    [JsonPropertyName("default_max_image_number")]
    public long DefaultMaxImageNumber { get; set; }

    [JsonPropertyName("default_image_number")]
    public long DefaultImageNumber { get; set; }

    [JsonPropertyName("available_aspect_ratios")]
    public string[] AvailableAspectRatios { get; set; }

    [JsonPropertyName("default_aspect_ratio")]
    public string DefaultAspectRatio { get; set; }

    [JsonPropertyName("default_inpaint_engine_version")]
    public string DefaultInpaintEngineVersion { get; set; }

    [JsonPropertyName("default_cfg_tsnr")]
    public long DefaultCfgTsnr { get; set; }

    [JsonPropertyName("default_overwrite_step")]
    public long DefaultOverwriteStep { get; set; }

    [JsonPropertyName("default_overwrite_switch")]
    public long DefaultOverwriteSwitch { get; set; }

    [JsonPropertyName("example_inpaint_prompts")]
    public string[] ExampleInpaintPrompts { get; set; }
}
