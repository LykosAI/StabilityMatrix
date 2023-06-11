using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Models.Api;

[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class A3Options
{
    [JsonPropertyName("samples_save")]
    public bool? SamplesSave { get; set; }

    [JsonPropertyName("samples_format")]
    public string? SamplesFormat { get; set; }

    [JsonPropertyName("samples_filename_pattern")]
    public string? SamplesFilenamePattern { get; set; }

    [JsonPropertyName("save_images_add_number")]
    public bool? SaveImagesAddNumber { get; set; }

    [JsonPropertyName("grid_save")]
    public bool? GridSave { get; set; }

    [JsonPropertyName("grid_format")]
    public string? GridFormat { get; set; }

    [JsonPropertyName("grid_extended_filename")]
    public bool? GridExtendedFilename { get; set; }

    [JsonPropertyName("grid_only_if_multiple")]
    public bool? GridOnlyIfMultiple { get; set; }

    [JsonPropertyName("grid_prevent_empty_spots")]
    public bool? GridPreventEmptySpots { get; set; }

    [JsonPropertyName("grid_zip_filename_pattern")]
    public string? GridZipFilenamePattern { get; set; }

    [JsonPropertyName("n_rows")]
    public int? NRows { get; set; }

    [JsonPropertyName("enable_pnginfo")]
    public bool? EnablePnginfo { get; set; }

    [JsonPropertyName("save_txt")]
    public bool? SaveTxt { get; set; }

    [JsonPropertyName("save_images_before_face_restoration")]
    public bool? SaveImagesBeforeFaceRestoration { get; set; }

    [JsonPropertyName("save_images_before_highres_fix")]
    public bool? SaveImagesBeforeHighresFix { get; set; }

    [JsonPropertyName("save_images_before_color_correction")]
    public bool? SaveImagesBeforeColorCorrection { get; set; }

    [JsonPropertyName("save_mask")]
    public bool? SaveMask { get; set; }

    [JsonPropertyName("save_mask_composite")]
    public bool? SaveMaskComposite { get; set; }

    [JsonPropertyName("jpeg_quality")]
    public int? JpegQuality { get; set; }

    [JsonPropertyName("webp_lossless")]
    public bool? WebpLossless { get; set; }

    [JsonPropertyName("export_for_4chan")]
    public bool? ExportFor4chan { get; set; }

    [JsonPropertyName("img_downscale_threshold")]
    public int? ImgDownscaleThreshold { get; set; }

    [JsonPropertyName("target_side_length")]
    public int? TargetSideLength { get; set; }

    [JsonPropertyName("img_max_size_mp")]
    public int? ImgMaxSizeMp { get; set; }

    [JsonPropertyName("use_original_name_batch")]
    public bool? UseOriginalNameBatch { get; set; }

    [JsonPropertyName("use_upscaler_name_as_suffix")]
    public bool? UseUpscalerNameAsSuffix { get; set; }

    [JsonPropertyName("save_selected_only")]
    public bool? SaveSelectedOnly { get; set; }

    [JsonPropertyName("save_init_img")]
    public bool? SaveInitImg { get; set; }

    [JsonPropertyName("temp_dir")]
    public string? TempDir { get; set; }

    [JsonPropertyName("clean_temp_dir_at_start")]
    public bool? CleanTempDirAtStart { get; set; }

    [JsonPropertyName("outdir_samples")]
    public string? OutdirSamples { get; set; }

    [JsonPropertyName("outdir_txt2img_samples")]
    public string? OutdirTxt2ImgSamples { get; set; }

    [JsonPropertyName("outdir_img2img_samples")]
    public string? OutdirImg2ImgSamples { get; set; }

    [JsonPropertyName("outdir_extras_samples")]
    public string? OutdirExtrasSamples { get; set; }

    [JsonPropertyName("outdir_grids")]
    public string? OutdirGrids { get; set; }

    [JsonPropertyName("outdir_txt2img_grids")]
    public string? OutdirTxt2ImgGrids { get; set; }

    [JsonPropertyName("outdir_img2img_grids")]
    public string? OutdirImg2ImgGrids { get; set; }

    [JsonPropertyName("outdir_save")]
    public string? OutdirSave { get; set; }

    [JsonPropertyName("outdir_init_images")]
    public string? OutdirInitImages { get; set; }

    [JsonPropertyName("save_to_dirs")]
    public bool? SaveToDirs { get; set; }

    [JsonPropertyName("grid_save_to_dirs")]
    public bool? GridSaveToDirs { get; set; }

    [JsonPropertyName("use_save_to_dirs_for_ui")]
    public bool? UseSaveToDirsForUi { get; set; }

    [JsonPropertyName("directories_filename_pattern")]
    public string? DirectoriesFilenamePattern { get; set; }

    [JsonPropertyName("directories_max_prompt_words")]
    public int? DirectoriesMaxPromptWords { get; set; }

    [JsonPropertyName("ESRGAN_tile")]
    public int? ESRGANTile { get; set; }

    [JsonPropertyName("ESRGAN_tile_overlap")]
    public int? ESRGANTileOverlap { get; set; }

    [JsonPropertyName("realesrgan_enabled_models")]
    public List<string>? RealEsrganEnabledModels { get; set; }

    [JsonPropertyName("upscaler_for_img2img")]
    public string? UpscalerForImg2img { get; set; }

    [JsonPropertyName("face_restoration_model")]
    public string? FaceRestorationModel { get; set; }

    [JsonPropertyName("code_former_weight")]
    public double? CodeFormerWeight { get; set; }

    [JsonPropertyName("face_restoration_unload")]
    public bool? FaceRestorationUnload { get; set; }

    [JsonPropertyName("show_warnings")]
    public bool? ShowWarnings { get; set; }

    [JsonPropertyName("memmon_poll_rate")]
    public int? MemmonPollRate { get; set; }

    [JsonPropertyName("samples_log_stdout")]
    public bool? SamplesLogStdout { get; set; }

    [JsonPropertyName("multiple_tqdm")]
    public bool? MultipleTqdm { get; set; }

    [JsonPropertyName("print_hypernet_extra")]
    public bool? PrintHypernetExtra { get; set; }

    [JsonPropertyName("list_hidden_files")]
    public bool? ListHiddenFiles { get; set; }

    [JsonPropertyName("unload_models_when_training")]
    public bool? UnloadModelsWhenTraining { get; set; }

    [JsonPropertyName("pin_memory")]
    public bool? PinMemory { get; set; }

    [JsonPropertyName("save_optimizer_state")]
    public bool? SaveOptimizerState { get; set; }

    [JsonPropertyName("save_training_settings_to_txt")]
    public bool? SaveTrainingSettingsToTxt { get; set; }

    [JsonPropertyName("dataset_filename_word_regex")]
    public string? DatasetFilenameWordRegex { get; set; }

    [JsonPropertyName("dataset_filename_join_string")]
    public string? DatasetFilenameJoinString { get; set; }

    [JsonPropertyName("training_image_repeats_per_epoch")]
    public int? TrainingImageRepeatsPerEpoch { get; set; }

    [JsonPropertyName("training_write_csv_every")]
    public int? TrainingWriteCsvEvery { get; set; }

    [JsonPropertyName("training_xattention_optimizations")]
    public bool? TrainingXattentionOptimizations { get; set; }

    [JsonPropertyName("training_enable_tensorboard")]
    public bool? TrainingEnableTensorboard { get; set; }

    [JsonPropertyName("training_tensorboard_save_images")]
    public bool? TrainingTensorboardSaveImages { get; set; }

    [JsonPropertyName("training_tensorboard_flush_every")]
    public int? TrainingTensorboardFlushEvery { get; set; }

    [JsonPropertyName("sd_model_checkpoint")]
    public string? SdModelCheckpoint { get; set; }

    [JsonPropertyName("sd_checkpoint_cache")]
    public int? SdCheckpointCache { get; set; }

    [JsonPropertyName("sd_vae_checkpoint_cache")]
    public int? SdVaeCheckpointCache { get; set; }

    [JsonPropertyName("sd_vae")]
    public string? SdVae { get; set; }

    [JsonPropertyName("sd_vae_as_default")]
    public bool? SdVaeAsDefault { get; set; }

    [JsonPropertyName("sd_unet")]
    public string? SdUnet { get; set; }

    [JsonPropertyName("inpainting_mask_weight")]
    public int? InpaintingMaskWeight { get; set; }

    [JsonPropertyName("initial_noise_multiplier")]
    public int? InitialNoiseMultiplier { get; set; }

    [JsonPropertyName("img2img_color_correction")]
    public bool? Img2imgColorCorrection { get; set; }

    [JsonPropertyName("img2img_fix_steps")]
    public bool? Img2imgFixSteps { get; set; }

    [JsonPropertyName("img2img_background_color")]
    public string? Img2ImgBackgroundColor { get; set; }

    [JsonPropertyName("enable_quantization")]
    public bool? EnableQuantization { get; set; }

    [JsonPropertyName("enable_emphasis")]
    public bool? EnableEmphasis { get; set; }

    [JsonPropertyName("enable_batch_seeds")]
    public bool? EnableBatchSeeds { get; set; }

    [JsonPropertyName("comma_padding_backtrack")]
    public int? CommaPaddingBacktrack { get; set; }

    [JsonPropertyName("CLIP_stop_at_last_layers")]
    public int? CLIPStopAtLastLayers { get; set; }

    [JsonPropertyName("upcast_attn")]
    public bool? UpcastAttn { get; set; }

    [JsonPropertyName("randn_source")]
    public string? RandNSource { get; set; }

    [JsonPropertyName("cross_attention_optimization")]
    public string? CrossAttentionOptimization { get; set; }

    [JsonPropertyName("s_min_uncond")]
    public int? SMinUncond { get; set; }

    [JsonPropertyName("token_merging_ratio")]
    public int? TokenMergingRatio { get; set; }

    [JsonPropertyName("token_merging_ratio_img2img")]
    public int? TokenMergingRatioImg2Img { get; set; }

    [JsonPropertyName("token_merging_ratio_hr")]
    public int? TokenMergingRatioHr { get; set; }

    [JsonPropertyName("pad_cond_uncond")]
    public bool? PadCondUncond { get; set; }

    [JsonPropertyName("experimental_persistent_cond_cache")]
    public bool? ExperimentalPersistentCondCache { get; set; }

    [JsonPropertyName("use_old_emphasis_implementation")]
    public bool? UseOldEmphasisImplementation { get; set; }

    [JsonPropertyName("use_old_karras_scheduler_sigmas")]
    public bool? UseOldKarrasSchedulerSigmas { get; set; }

    [JsonPropertyName("no_dpmpp_sde_batch_determinism")]
    public bool? NoDpmppSdeBatchDeterminism { get; set; }

    [JsonPropertyName("use_old_hires_fix_width_height")]
    public bool? UseOldHiresFixWidthHeight { get; set; }

    [JsonPropertyName("dont_fix_second_order_samplers_schedule")]
    public bool? DontFixSecondOrderSamplersSchedule { get; set; }

    [JsonPropertyName("hires_fix_use_firstpass_conds")]
    public bool? HiresFixUseFirstpassConds { get; set; }

    [JsonPropertyName("interrogate_keep_models_in_memory")]
    public bool? InterrogateKeepModelsInMemory { get; set; }

    [JsonPropertyName("interrogate_return_ranks")]
    public bool? InterrogateReturnRanks { get; set; }

    [JsonPropertyName("interrogate_clip_num_beams")]
    public int? InterrogateClipNumBeams { get; set; }

    [JsonPropertyName("interrogate_clip_min_length")]
    public int? InterrogateClipMinLength { get; set; }

    [JsonPropertyName("interrogate_clip_max_length")]
    public int? InterrogateClipMaxLength { get; set; }

    [JsonPropertyName("interrogate_clip_dict_limit")]
    public int? InterrogateClipDictLimit { get; set; }

    [JsonPropertyName("interrogate_clip_skip_categories")]
    public List<string>? InterrogateClipSkipCategories { get; set; }

    [JsonPropertyName("interrogate_deepbooru_score_threshold")]
    public double? InterrogateDeepbooruScoreThreshold { get; set; }

    [JsonPropertyName("deepbooru_sort_alpha")]
    public bool? DeepbooruSortAlpha { get; set; }

    [JsonPropertyName("deepbooru_use_spaces")]
    public bool? DeepbooruUseSpaces { get; set; }

    [JsonPropertyName("deepbooru_escape")]
    public bool? DeepbooruEscape { get; set; }

    [JsonPropertyName("deepbooru_filter_tags")]
    public string? DeepbooruFilterTags { get; set; }

    [JsonPropertyName("extra_networks_show_hidden_directories")]
    public bool? ExtraNetworksShowHiddenDirectories { get; set; }

    [JsonPropertyName("extra_networks_hidden_models")]
    public string? ExtraNetworksHiddenModels { get; set; }

    [JsonPropertyName("extra_networks_default_view")]
    public string? ExtraNetworksDefaultView { get; set; }

    [JsonPropertyName("extra_networks_default_multiplier")]
    public int? ExtraNetworksDefaultMultiplier { get; set; }

    [JsonPropertyName("extra_networks_card_width")]
    public int? ExtraNetworksCardWidth { get; set; }

    [JsonPropertyName("extra_networks_card_height")]
    public int? ExtraNetworksCardHeight { get; set; }

    [JsonPropertyName("extra_networks_add_text_separator")]
    public string? ExtraNetworksAddTextSeparator { get; set; }

    [JsonPropertyName("ui_extra_networks_tab_reorder")]
    public string? UiExtraNetworksTabReorder { get; set; }

    [JsonPropertyName("sd_hypernetwork")]
    public string? SdHypernetwork { get; set; }

    [JsonPropertyName("localization")]
    public string? Localization { get; set; }

    [JsonPropertyName("gradio_theme")]
    public string? GradioTheme { get; set; }

    [JsonPropertyName("img2img_editor_height")]
    public int? Img2ImgEditorHeight { get; set; }

    [JsonPropertyName("return_grid")]
    public bool? ReturnGrid { get; set; }

    [JsonPropertyName("return_mask")]
    public bool? ReturnMask { get; set; }

    [JsonPropertyName("return_mask_composite")]
    public bool? ReturnMaskComposite { get; set; }

    [JsonPropertyName("do_not_show_images")]
    public bool? DoNotShowImages { get; set; }

    [JsonPropertyName("send_seed")]
    public bool? SendSeed { get; set; }

    [JsonPropertyName("send_size")]
    public bool? SendSize { get; set; }

    [JsonPropertyName("font")]
    public string? Font { get; set; }

    [JsonPropertyName("js_modal_lightbox")]
    public bool? JsModalLightbox { get; set; }

    [JsonPropertyName("js_modal_lightbox_initially_zoomed")]
    public bool? JsModalLightboxInitiallyZoomed { get; set; }

    [JsonPropertyName("js_modal_lightbox_gamepad")]
    public bool? JsModalLightboxGamepad { get; set; }

    [JsonPropertyName("js_modal_lightbox_gamepad_repeat")]
    public int? JsModalLightboxGamepadRepeat { get; set; }

    [JsonPropertyName("show_progress_in_title")]
    public bool? ShowProgressInTitle { get; set; }

    [JsonPropertyName("samplers_in_dropdown")]
    public bool? SamplersInDropdown { get; set; }

    [JsonPropertyName("dimensions_and_batch_together")]
    public bool? DimensionsAndBatchTogether { get; set; }

    [JsonPropertyName("keyedit_precision_attention")]
    public double? KeyEditPrecisionAttention { get; set; }

    [JsonPropertyName("keyedit_precision_extra")]
    public double? KeyEditPrecisionExtra { get; set; }

    [JsonPropertyName("keyedit_delimiters")]
    public string? KeyEditDelimiters { get; set; }

    [JsonPropertyName("hires_fix_show_sampler")]
    public bool? HiresFixShowSampler { get; set; }

    [JsonPropertyName("hires_fix_show_prompts")]
    public bool? HiresFixShowPrompts { get; set; }

    [JsonPropertyName("disable_token_counters")]
    public bool? DisableTokenCounters { get; set; }

    [JsonPropertyName("add_model_hash_to_info")]
    public bool? AddModelHashToInfo { get; set; }

    [JsonPropertyName("add_model_name_to_info")]
    public bool? AddModelNameToInfo { get; set; }

    [JsonPropertyName("add_version_to_infotext")]
    public bool? AddVersionToInfotext { get; set; }

    [JsonPropertyName("disable_weights_auto_swap")]
    public bool? DisableWeightsAutoSwap { get; set; }

    [JsonPropertyName("infotext_styles")]
    public string? InfotextStyles { get; set; }

    [JsonPropertyName("show_progressbar")]
    public bool? ShowProgressbar { get; set; }

    [JsonPropertyName("live_previews_enable")]
    public bool? LivePreviewsEnable { get; set; }

    [JsonPropertyName("live_previews_image_format")]
    public string? LivePreviewsImageFormat { get; set; }

    [JsonPropertyName("show_progress_grid")]
    public bool? ShowProgressGrid { get; set; }

    [JsonPropertyName("show_progress_every_n_steps")]
    public int? ShowProgressEveryNSteps { get; set; }

    [JsonPropertyName("show_progress_type")]
    public string? ShowProgressType { get; set; }

    [JsonPropertyName("live_preview_content")]
    public string? LivePreviewContent { get; set; }

    [JsonPropertyName("live_preview_refresh_period")]
    public int? LivePreviewRefreshPeriod { get; set; }

    // TODO: hide_samplers

    [JsonPropertyName("eta_ddim")]
    public int? EtaDdim { get; set; }

    [JsonPropertyName("eta_ancestral")]
    public int? EtaAncestral { get; set; }

    [JsonPropertyName("ddim_discretize")]
    public string? DDIMDiscretize { get; set; }

    [JsonPropertyName("s_churn")]
    public int? SChurn { get; set; }

    [JsonPropertyName("s_tmin")]
    public int? STmin { get; set; }

    [JsonPropertyName("s_noise")]
    public int? SNoise { get; set; }

    [JsonPropertyName("k_sched_type")]
    public string? KSchedType { get; set; }

    [JsonPropertyName("sigma_min")]
    public int? SigmaMin { get; set; }

    [JsonPropertyName("sigma_max")]
    public int? SigmaMax { get; set; }

    [JsonPropertyName("rho")]
    public int? Rho { get; set; }

    [JsonPropertyName("eta_noise_seed_delta")]
    public int? EtaNoiseSeedDelta { get; set; }

    [JsonPropertyName("always_discard_next_to_last_sigma")]
    public bool? AlwaysDiscardNextToLastSigma { get; set; }

    [JsonPropertyName("uni_pc_variant")]
    public string? UniPcVariant { get; set; }

    [JsonPropertyName("uni_pc_skip_type")]
    public string? UniPcSkipType { get; set; }

    [JsonPropertyName("uni_pc_order")]
    public int? UniPcOrder { get; set; }

    [JsonPropertyName("uni_pc_lower_order_final")]
    public bool? UniPcLowerOrderFinal { get; set; }

    // TODO: postprocessing_enable_in_main_ui
    // TODO: postprocessing_operation_order

    [JsonPropertyName("upscaling_max_images_in_cache")]
    public int? UpscalingMaxImagesInCache { get; set; }

    [JsonPropertyName("disabled_extensions")]
    public List<string>? DisabledExtensions { get; set; }

    [JsonPropertyName("disable_all_extensions")]
    public string? DisableAllExtensions { get; set; }

    [JsonPropertyName("restore_config_state_file")]
    public string? RestoreConfigStateFile { get; set; }

    [JsonPropertyName("sd_checkpoint_hash")]
    public string? SdCheckpointHash { get; set; }

    [JsonPropertyName("sd_lora")]
    public string? SdLora { get; set; }

    [JsonPropertyName("lora_preferred_name")]
    public string? LoraPreferredName { get; set; }

    [JsonPropertyName("lora_add_hashes_to_infotext")]
    public bool? LoraAddHashesToInfotext { get; set; }

    [JsonPropertyName("lora_functional")]
    public bool? LoraFunctional { get; set; }

    [JsonPropertyName("canvas_hotkey_move")]
    public string? CanvasHotkeyMove { get; set; }

    [JsonPropertyName("canvas_hotkey_fullscreen")]
    public string? CanvasHotkeyFullscreen { get; set; }

    [JsonPropertyName("canvas_hotkey_reset")]
    public string? CanvasHotkeyReset { get; set; }

    [JsonPropertyName("canvas_hotkey_overlap")]
    public string? CanvasHotkeyOverlap { get; set; }

    [JsonPropertyName("canvas_show_tooltip")]
    public bool? CanvasShowTooltip { get; set; }

    [JsonPropertyName("canvas_swap_controls")]
    public bool? CanvasSwapControls { get; set; }
    
    // TODO: List<object> ExtraOptions

    [JsonPropertyName("extra_options_accordion")]
    public bool? ExtraOptionsAccordion { get; set; }
}
