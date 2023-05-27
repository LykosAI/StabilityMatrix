using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;


public class TextToImageRequest
{
    [JsonPropertyName("enable_hr")]
    public bool? EnableHr { get; set; }

    [JsonPropertyName("denoising_strength")]
    public int? DenoisingStrength { get; set; }

    [JsonPropertyName("firstphase_width")]
    public int? FirstPhaseWidth { get; set; }

    [JsonPropertyName("firstphase_height")]
    public int? FirstPhaseHeight { get; set; }

    [JsonPropertyName("hr_scale")]
    public int? HrScale { get; set; }

    [JsonPropertyName("hr_upscaler")]
    public string? HrUpscaler { get; set; }

    [JsonPropertyName("hr_second_pass_steps")]
    public int? HrSecondPassSteps { get; set; }

    [JsonPropertyName("hr_resize_x")]
    public int? HrResizeX { get; set; }

    [JsonPropertyName("hr_resize_y")]
    public int? HrResizeY { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("styles")]
    public string?[] Styles { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("subseed")]
    public int? Subseed { get; set; }

    [JsonPropertyName("subseed_strength")]
    public int? SubseedStrength { get; set; }

    [JsonPropertyName("seed_resize_from_h")]
    public int? SeedResizeFromH { get; set; }

    [JsonPropertyName("seed_resize_from_w")]
    public int? SeedResizeFromW { get; set; }

    [JsonPropertyName("sampler_name")]
    public string? SamplerName { get; set; }

    [JsonPropertyName("batch_size")]
    public int? BatchSize { get; set; }

    [JsonPropertyName("n_iter")]
    public int? NIter { get; set; }

    [JsonPropertyName("steps")]
    public int? Steps { get; set; }

    [JsonPropertyName("cfg_scale")]
    public int? CfgScale { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("restore_faces")]
    public bool? RestoreFaces { get; set; }

    [JsonPropertyName("tiling")]
    public bool? Tiling { get; set; }

    [JsonPropertyName("do_not_save_samples")]
    public bool? DoNotSaveSamples { get; set; }

    [JsonPropertyName("do_not_save_grid")]
    public bool? DoNotSaveGrid { get; set; }

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("eta")]
    public int? Eta { get; set; }

    [JsonPropertyName("s_min_uncond")]
    public int? SMinUncond { get; set; }

    [JsonPropertyName("s_churn")]
    public int? SChurn { get; set; }

    [JsonPropertyName("s_tmax")]
    public int? STmax { get; set; }

    [JsonPropertyName("s_tmin")]
    public int? STmin { get; set; }

    [JsonPropertyName("s_noise")]
    public int? SNoise { get; set; }

    [JsonPropertyName("override_settings")]
    public Dictionary<string, string>? OverrideSettings { get; set; }

    [JsonPropertyName("override_settings_restore_afterwards")]
    public bool? OverrideSettingsRestoreAfterwards { get; set; }

    [JsonPropertyName("script_args")]
    public string[]? ScriptArgs { get; set; }

    [JsonPropertyName("sampler_index")]
    public string? SamplerIndex { get; set; }

    [JsonPropertyName("script_name")]
    public string? ScriptName { get; set; }

    [JsonPropertyName("send_images")]
    public bool? SendImages { get; set; }

    [JsonPropertyName("save_images")]
    public bool? SaveImages { get; set; }

    [JsonPropertyName("alwayson_scripts")]
    public Dictionary<string, string>? AlwaysOnScripts { get; set; }
}
