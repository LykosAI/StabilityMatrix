using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Dialogs;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(VideoModelCard))]
[ManagedService]
[RegisterTransient<VideoModelCardViewModel>]
public partial class VideoModelCardViewModel(
    IInferenceClientManager clientManager,
    IServiceManager<ViewModelBase> vmFactory
) : LoadableViewModelBase, IParametersLoadableState, IComfyStep
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecommendedDefaults))]
    [NotifyPropertyChangedFor(nameof(RecommendedDefaultsToolTip))]
    [NotifyPropertyChangedFor(nameof(ShowEnableLowNoisePrompt))]
    [NotifyPropertyChangedFor(nameof(ResolvedWorkflowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowMoeExpertControls))]
    [NotifyPropertyChangedFor(nameof(ShowLatentSourceMenu))]
    [NotifyPropertyChangedFor(nameof(ShowShiftControl))]
    [NotifyPropertyChangedFor(nameof(ShowLowNoiseSections))]
    [NotifyPropertyChangedFor(nameof(ShowAudioVae))]
    [NotifyPropertyChangedFor(nameof(ShowModelOptionsButton))]
    [NotifyPropertyChangedFor(nameof(ShowLowNoiseSectionsSeparator))]
    [NotifyPropertyChangedFor(nameof(ShowClipVisionRow))]
    [NotifyPropertyChangedFor(nameof(WorkflowProfileStatusText))]
    private HybridModelFile? selectedModel;

    /// <summary>
    /// The model-family workflow this tab runs. Auto resolves from the selected UNet via
    /// <see cref="VideoWorkflowProfile.Detect"/>; family-specific controls and graph steps key
    /// off <see cref="ResolvedWorkflowProfile"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResolvedWorkflowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowMoeExpertControls))]
    [NotifyPropertyChangedFor(nameof(ShowLatentSourceMenu))]
    [NotifyPropertyChangedFor(nameof(ShowShiftControl))]
    [NotifyPropertyChangedFor(nameof(ShowLowNoiseSections))]
    [NotifyPropertyChangedFor(nameof(ShowEnableLowNoisePrompt))]
    [NotifyPropertyChangedFor(nameof(ShowWorkflowProfileStatus))]
    [NotifyPropertyChangedFor(nameof(ShowAudioVae))]
    [NotifyPropertyChangedFor(nameof(ShowModelOptionsButton))]
    [NotifyPropertyChangedFor(nameof(ShowLowNoiseSectionsSeparator))]
    [NotifyPropertyChangedFor(nameof(ShowClipVisionRow))]
    [NotifyPropertyChangedFor(nameof(WorkflowProfileStatusText))]
    private VideoWorkflowProfile selectedWorkflowProfile = VideoWorkflowProfile.Auto;

    [JsonIgnore]
    public IReadOnlyList<VideoWorkflowProfile> WorkflowProfiles => VideoWorkflowProfile.All;

    /// <summary>The concrete profile in effect — never <see cref="VideoWorkflowProfile.Auto"/>.</summary>
    [JsonIgnore]
    public VideoWorkflowProfile ResolvedWorkflowProfile =>
        SelectedWorkflowProfile == VideoWorkflowProfile.Auto
            ? VideoWorkflowProfile.Detect(SelectedModel)
            : SelectedWorkflowProfile;

    [JsonIgnore]
    public bool ShowMoeExpertControls => ResolvedWorkflowProfile.SupportsMoeExperts;

    [JsonIgnore]
    public bool ShowLatentSourceMenu => ResolvedWorkflowProfile.ShowsLatentSourceMode;

    [JsonIgnore]
    public bool ShowShiftControl => ResolvedWorkflowProfile.UsesModelSamplingShift;

    /// <summary>The dual-expert rows only apply when the family supports the MoE pair.</summary>
    [JsonIgnore]
    public bool ShowLowNoiseSections => IsLowNoiseModelEnabled && ShowMoeExpertControls;

    /// <summary>The options flyout only holds family-specific items; hide the gear when empty.</summary>
    [JsonIgnore]
    public bool ShowModelOptionsButton => ShowMoeExpertControls || ShowLatentSourceMenu;

    /// <summary>Separator between the flyout's dual-expert and latent sections.</summary>
    [JsonIgnore]
    public bool ShowLowNoiseSectionsSeparator => ShowMoeExpertControls && ShowLatentSourceMenu;

    [JsonIgnore]
    public bool ShowAudioVae => ResolvedWorkflowProfile.UsesAudioVae;

    /// <summary>CLIP Vision applies when the tab wants it (i2v) and the family uses it.</summary>
    [JsonIgnore]
    public bool ShowClipVisionRow => IsClipVisionEnabled && ResolvedWorkflowProfile.UsesClipVision;

    [JsonIgnore]
    public bool ShowWorkflowProfileStatus => SelectedWorkflowProfile == VideoWorkflowProfile.Auto;

    [JsonIgnore]
    public string WorkflowProfileStatusText =>
        $"Auto — using the {ResolvedWorkflowProfile.DisplayName} workflow";

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        // A different model may not be a 14B expert (or may be the other half of a pair) — re-offer the nudge.
        IsLowNoisePromptDismissed = false;

        AutoFillProfileCompanions();
    }

    partial void OnSelectedWorkflowProfileChanged(VideoWorkflowProfile value)
    {
        AutoFillProfileCompanions();
    }

    /// <summary>
    /// Fills the VAE / audio VAE / text encoder slots from installed files matching the
    /// resolved profile's expected filenames. Only empty slots are filled — an explicit user
    /// (or loaded-project) selection is never replaced.
    /// </summary>
    private void AutoFillProfileCompanions()
    {
        var profile = ResolvedWorkflowProfile;

        if (SelectedVae is null && profile.VaeAutoFill(SelectedModel) is { } vaeMatch)
        {
            SelectedVae = ClientManager.VaeModels.FirstOrDefault(m =>
                m.Local is not null && vaeMatch(m.FileName)
            );
        }

        if (SelectedAudioVae is null && profile.AudioVaeAutoFill is { } audioVaeMatch)
        {
            SelectedAudioVae = ClientManager.VaeModels.FirstOrDefault(m =>
                m.Local is not null && audioVaeMatch(m.FileName)
            );
        }

        if (SelectedClipModel is null && profile.TextEncoderAutoFill is { } encoderMatch)
        {
            SelectedClipModel = ClientManager.ClipModels.FirstOrDefault(m =>
                m.Local is not null && encoderMatch(m.FileName)
            );
        }
    }

    /// <summary>
    /// Optional low-noise expert model for the Wan 2.2 14B MoE. When <see cref="IsLowNoiseModelEnabled"/>
    /// is set and this is selected, <see cref="SelectedModel"/> acts as the high-noise expert and the
    /// sampler runs a two-pass high-noise -> low-noise generation.
    /// </summary>
    [ObservableProperty]
    private HybridModelFile? selectedLowNoiseModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryModelLabel))]
    [NotifyPropertyChangedFor(nameof(ShowEnableLowNoisePrompt))]
    [NotifyPropertyChangedFor(nameof(ShowLowNoiseSections))]
    private bool isLowNoiseModelEnabled;

    [ObservableProperty]
    private bool isAdvancedExpanded;

    /// <summary>
    /// Set when the user closes the "enable the second expert" nudge, so it stays hidden until they pick a
    /// different model. Not persisted — the prompt is cheap to re-show next session.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEnableLowNoisePrompt))]
    private bool isLowNoisePromptDismissed;

    /// <summary>
    /// Label for the primary model row. In dual-expert mode this becomes "High-Noise" so it reads as a
    /// symmetric pair with the "Low-Noise" row; otherwise it's the plain "Model" label.
    /// </summary>
    public string PrimaryModelLabel => IsLowNoiseModelEnabled ? "High-Noise" : Resources.Label_Model;

    /// <summary>
    /// Nudge shown when the selected primary model looks like one half of a Wan 2.2 14B MoE expert pair and
    /// dual-expert mode isn't on yet. Enabling it (see <see cref="EnableLowNoiseExpertCommand"/>) turns on the
    /// low-noise expert and auto-picks its counterpart when present.
    /// </summary>
    public bool ShowEnableLowNoisePrompt =>
        ResolvedWorkflowProfile.SupportsMoeExperts
        && !IsLowNoiseModelEnabled
        && !IsLowNoisePromptDismissed
        && AutoDetectIsWan2214BExpert();

    partial void OnIsLowNoiseModelEnabledChanged(bool value)
    {
        // Keep the LoRA section title symmetric with the Low-Noise LoRAs section in dual-expert mode.
        ExtraNetworksStackCardViewModel.Title = value ? "High-Noise LoRAs" : Resources.Label_ExtraNetworks;
    }

    /// <summary>
    /// Heuristic for a Wan 2.2 14B MoE expert, from the selected UNet name only. The MoE ships as paired
    /// <c>high_noise</c>/<c>low_noise</c> 14B experts; the 5B is single-model TI2V, so it's excluded.
    /// </summary>
    private bool AutoDetectIsWan2214BExpert()
    {
        if (SelectedModel?.FileName is not { } fileName || string.IsNullOrEmpty(fileName))
            return false;

        var name = fileName.ToLowerInvariant();
        if (name.Contains("ti2v") || name.Contains("5b"))
            return false;

        return name.Contains("high_noise") || name.Contains("low_noise");
    }

    /// <summary>
    /// Turns on the low-noise expert from the nudge and, when the high-noise counterpart can be matched by
    /// name (<c>high_noise</c> -&gt; <c>low_noise</c>), pre-selects it so the user doesn't have to.
    /// </summary>
    [RelayCommand]
    private void EnableLowNoiseExpert()
    {
        IsLowNoiseModelEnabled = true;

        if (SelectedLowNoiseModel is null && TryFindLowNoiseCounterpart() is { } counterpart)
        {
            SelectedLowNoiseModel = counterpart;
        }
    }

    /// <summary>Dismisses the nudge until a different model is selected (the InfoBar's close button).</summary>
    [RelayCommand]
    private void DismissLowNoisePrompt() => IsLowNoisePromptDismissed = true;

    private HybridModelFile? TryFindLowNoiseCounterpart()
    {
        if (SelectedModel?.FileName is not { } fileName)
            return null;

        foreach (var (from, to) in new[] { ("high_noise", "low_noise"), ("high", "low") })
        {
            if (!fileName.Contains(from, StringComparison.OrdinalIgnoreCase))
                continue;

            var candidate = fileName.Replace(from, to, StringComparison.OrdinalIgnoreCase);
            if (
                ClientManager.UnetModels.FirstOrDefault(m =>
                    string.Equals(m.FileName, candidate, StringComparison.OrdinalIgnoreCase)
                ) is
                { } match
            )
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Fraction of sampling steps handled by the high-noise expert before switching to the low-noise
    /// expert (Wan 2.2 14B MoE). 0.5 splits the steps evenly, matching the native ComfyUI template.
    /// </summary>
    [ObservableProperty]
    private double moeBoundary = 0.5d;

    [ObservableProperty]
    private HybridModelFile? selectedClipModel;

    [ObservableProperty]
    private HybridModelFile? selectedClipVisionModel;

    [ObservableProperty]
    private HybridModelFile? selectedVae;

    /// <summary>
    /// Optional audio VAE for families with native audio (MiniMax H3). When unset the
    /// generation still runs, video-only.
    /// </summary>
    [ObservableProperty]
    private HybridModelFile? selectedAudioVae;

    [ObservableProperty]
    private string? selectedDType = "fp8_e4m3fn_fast";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClipVisionRow))]
    private bool isClipVisionEnabled;

    [ObservableProperty]
    private double shift = 8.0d;

    /// <summary>
    /// Controls which empty-latent node the Wan text-to-video tab emits. The Wan 2.2 TI2V 5B model needs a
    /// 48-channel / 16x latent (<c>Wan22ImageToVideoLatent</c>); the 14B and Wan 2.1 models need the
    /// 16-channel / 8x <c>EmptyHunyuanLatentVideo</c>. <see cref="WanLatentSourceMode.Auto"/> infers this
    /// from the selected UNet name; the other modes force one or the other.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecommendedDefaultsToolTip))]
    private WanLatentSourceMode latentSourceMode = WanLatentSourceMode.Auto;

    public IInferenceClientManager ClientManager { get; } = clientManager;

    /// <summary>
    /// True when the Wan 2.2 TI2V 5B latent (<c>Wan22ImageToVideoLatent</c>) should be used instead of the
    /// default <c>EmptyHunyuanLatentVideo</c>. Resolved from <see cref="LatentSourceMode"/>, falling back to
    /// name-based auto-detection of the selected UNet.
    /// </summary>
    public bool UseWan22FiveBLatent =>
        LatentSourceMode switch
        {
            WanLatentSourceMode.Wan22FiveB => true,
            WanLatentSourceMode.Legacy => false,
            _ => AutoDetectIsWan22FiveB(),
        };

    /// <summary>
    /// Heuristic for the Wan 2.2 TI2V 5B single-model workflow, based on the selected UNet name only. The 5B
    /// model is named like <c>wan2.2_ti2v_5B_*</c>; the 14B experts are <c>t2v</c>/<c>i2v</c>. The VAE is
    /// deliberately not considered — it usually lags a step behind when switching models, which made the
    /// detection (and the ✨ button) appear to "do nothing". Use the override for renamed files.
    /// </summary>
    private bool AutoDetectIsWan22FiveB()
    {
        if (SelectedModel?.FileName is not { } fileName || string.IsNullOrEmpty(fileName))
            return false;

        var name = fileName.ToLowerInvariant();
        // "ti2v" is unique to the 5B model; "5b" covers GGUF/community repacks.
        return name.Contains("ti2v") || name.Contains("5b");
    }

    /// <summary>
    /// Raised when the user clicks the recommended-defaults (✨) button. The bool is
    /// <see cref="UseWan22FiveBLatent"/> so the owning tab can apply the matching resolution / length / fps
    /// for the 5B vs 14B / 2.1 workflow. Mirrors <c>ModelCardViewModel.RecommendedDefaultsRequested</c>.
    /// </summary>
    public event Action<bool>? RecommendedDefaultsRequested;

    /// <summary>A model must be selected before we can suggest a matching workflow.</summary>
    public bool HasRecommendedDefaults => SelectedModel is not null;

    public string RecommendedDefaultsToolTip =>
        ResolvedWorkflowProfile == VideoWorkflowProfile.MiniMaxH3
            ? "Apply recommended MiniMax H3 settings: 1344×768 · 124 frames · 24 fps mp4 · res_multistep / simple / 20 steps / CFG 1"
        : UseWan22FiveBLatent
            ? "Apply recommended Wan 2.2 5B settings: 1280×704 · 121 frames · 24 fps · uni_pc / simple / 20 steps / CFG 5"
        : "Apply recommended Wan 14B / 2.1 settings: 832×480 · 81 frames · 16 fps · uni_pc / simple / 20 steps / CFG 5";

    [RelayCommand]
    private void ApplyRecommendedDefaults()
    {
        if (HasRecommendedDefaults)
        {
            RecommendedDefaultsRequested?.Invoke(UseWan22FiveBLatent);
        }
    }

    public StackEditableCardViewModel ExtraNetworksStackCardViewModel { get; } =
        new(vmFactory) { Title = Resources.Label_ExtraNetworks, AvailableModules = [typeof(LoraModule)] };

    /// <summary>
    /// LoRAs applied to the Wan 2.2 14B low-noise expert only. The main
    /// <see cref="ExtraNetworksStackCardViewModel"/> targets the high-noise (primary) model.
    /// </summary>
    public StackEditableCardViewModel LowNoiseExtraNetworksStackCardViewModel { get; } =
        new(vmFactory) { Title = "Low-Noise LoRAs", AvailableModules = [typeof(LoraModule)] };

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e4m3fn_fast", "fp8_e5m2"];

    [RelayCommand]
    private async Task OpenModelPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Model";
        // VideoModelCard only uses UNet models
        pickerVm.Source = ModelPickerSource.CheckpointAndUnet;
        pickerVm.ShowUnetsOnly = true;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedModel = selected;
            }
        }
    }

    [RelayCommand]
    private async Task OpenLowNoiseModelPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Low-Noise Model";
        pickerVm.Source = ModelPickerSource.CheckpointAndUnet;
        pickerVm.ShowUnetsOnly = true;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedLowNoiseModel = selected;
            }
        }
    }

    [RelayCommand]
    private async Task OpenVaePickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select VAE";
        pickerVm.Source = ModelPickerSource.Vae;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedVae = selected;
            }
        }
    }

    [RelayCommand]
    private async Task OpenAudioVaePickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Audio VAE";
        pickerVm.Source = ModelPickerSource.Vae;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedAudioVae = selected;
            }
        }
    }

    [RelayCommand]
    private async Task OpenClipPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Text Encoder";
        pickerVm.Source = ModelPickerSource.Clip;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedClipModel = selected;
            }
        }
    }

    [RelayCommand]
    private async Task OpenClipVisionPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select CLIP Vision";
        pickerVm.Source = ModelPickerSource.ClipVision;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedClipVisionModel = selected;
            }
        }
    }

    public async Task<bool> ValidateModel()
    {
        if (SelectedModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a model to continue.",
                "No Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (SelectedVae == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a VAE model to continue.",
                "No VAE Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (SelectedClipModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a CLIP model to continue.",
                "No CLIP Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (ShowClipVisionRow && SelectedClipVisionModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a CLIP Vision model to continue.",
                "No CLIP Vision Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        if (IsLowNoiseModelEnabled && SelectedLowNoiseModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a low-noise model to continue, or disable the low-noise expert.",
                "No Low-Noise Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Key under which the optional Wan 2.2 14B low-noise expert is stored in the builder's
    /// model connections, so shared model patches (LoRAs etc.) apply to it.
    /// </summary>
    public const string LowNoiseModelKey = "WanLowNoise";

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var profile = ResolvedWorkflowProfile;

        // Loads a UNet (GGUF or safetensors) and, for families that use it, applies the
        // ModelSamplingSD3 shift. Used for both the high-noise and (optional) low-noise
        // expert of the Wan 2.2 14B MoE.
        ModelNodeConnection LoadUnetModel(HybridModelFile model)
        {
            ComfyTypedNodeBase<ModelNodeConnection> modelLoader;
            if (model.RelativePath.EndsWith("gguf", StringComparison.OrdinalIgnoreCase))
            {
                modelLoader = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.UnetLoaderGGUF
                    {
                        Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UnetLoaderGGUF)),
                        UnetName = model.RelativePath,
                    }
                );
            }
            else
            {
                modelLoader = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.UNETLoader
                    {
                        Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                        UnetName = model.RelativePath,
                        WeightDtype = SelectedDType ?? "fp8_e4m3fn_fast",
                    }
                );
            }

            if (!profile.UsesModelSamplingShift)
                return modelLoader.Output;

            var modelSamplingSd3 = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ModelSamplingSD3
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.ModelSamplingSD3)),
                    Model = modelLoader.Output,
                    Shift = Shift,
                }
            );

            return modelSamplingSd3.Output;
        }

        var highNoiseModel = SelectedModel ?? throw new ValidationException("Model not selected");
        e.Builder.Connections.Base.Model = LoadUnetModel(highNoiseModel);

        // Wan 2.2 14B MoE: load the optional low-noise expert. Kept OUT of the shared model
        // dictionary so the main Extra Networks LoRAs target only the high-noise (primary) model;
        // the low-noise expert gets its own LoRA stack applied below. Its Clip is wired up after the
        // CLIP loader. The Wan sampler reads the final model from Connections.WanLowNoiseModel.
        ModelConnections? lowNoiseConnections = null;
        if (
            profile.SupportsMoeExperts
            && IsLowNoiseModelEnabled
            && SelectedLowNoiseModel is { } lowNoiseModel
        )
        {
            lowNoiseConnections = new ModelConnections(LowNoiseModelKey)
            {
                Model = LoadUnetModel(lowNoiseModel),
            };
            e.Builder.Connections.WanMoeBoundary = MoeBoundary;
        }

        var clipName =
            SelectedClipModel?.RelativePath ?? throw new ValidationException("No Clip Model Selected");

        var clipType =
            profile.ClipType
            ?? throw new InvalidOperationException("Resolved workflow profile has no clip type");

        // The GGUF loader variant can also load .safetensors encoders
        var clipOutput = SelectedClipModel is { IsGguf: true }
            ? e
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoaderGGUF
                    {
                        Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoaderGGUF)),
                        ClipName = clipName,
                        Type = clipType,
                    }
                )
                .Output
            : e
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.CLIPLoader
                    {
                        Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                        ClipName = clipName,
                        Type = clipType,
                    }
                )
                .Output;

        e.Builder.Connections.Base.Clip = clipOutput;

        // Share the text encoder with the low-noise expert so its model-side LoRA patches apply.
        if (lowNoiseConnections is not null)
        {
            lowNoiseConnections.Clip = clipOutput;
        }

        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected"),
            }
        );
        e.Builder.Connections.Base.VAE = vaeLoader.Output;
        e.Builder.Connections.PrimaryVAE = vaeLoader.Output;

        // High-noise / primary model LoRAs (low-noise expert isn't in Models, so it's untouched here).
        if (ExtraNetworksStackCardViewModel.Cards.OfType<LoraModule>().Any(x => x.IsEnabled))
        {
            ExtraNetworksStackCardViewModel.ApplyStep(e);
        }

        // Apply the low-noise expert's own LoRAs to that model only, then hand the patched model to
        // the sampler. The generic LoRA module patches every model in the builder dictionary, so we
        // briefly swap in just the low-noise model around the call.
        if (lowNoiseConnections is not null)
        {
            if (LowNoiseExtraNetworksStackCardViewModel.Cards.OfType<LoraModule>().Any(x => x.IsEnabled))
            {
                var savedModels = e.Builder.Connections.Models.ToList();
                try
                {
                    e.Builder.Connections.Models.Clear();
                    e.Builder.Connections.Models[LowNoiseModelKey] = lowNoiseConnections;

                    LowNoiseExtraNetworksStackCardViewModel.ApplyStep(e);
                }
                finally
                {
                    // Always restore the original models, even if lora application throws,
                    // so the builder dictionary isn't left in the temporary swapped state.
                    e.Builder.Connections.Models.Clear();
                    foreach (var (key, value) in savedModels)
                    {
                        e.Builder.Connections.Models[key] = value;
                    }
                }
            }

            e.Builder.Connections.WanLowNoiseModel = lowNoiseConnections.Model;
        }

        if (SelectedAudioVae is { } audioVae && profile.UsesAudioVae)
        {
            var audioVaeLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.VAELoader
                {
                    Name = e.Nodes.GetUniqueName("AudioVAELoader"),
                    VaeName = audioVae.RelativePath,
                }
            );
            e.Builder.Connections.AudioVAE = audioVaeLoader.Output;
        }

        if (!ShowClipVisionRow)
            return;

        var clipVisionLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPVisionLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPVisionLoader)),
                ClipName =
                    SelectedClipVisionModel?.RelativePath
                    ?? throw new ValidationException("No Clip Vision Model Selected"),
            }
        );

        e.Builder.Connections.BaseClipVision = clipVisionLoader.Output;
        e.Builder.Connections.Base.ClipVision = clipVisionLoader.Output;
    }

    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.UnetModels;

        HybridModelFile? model;

        // First try hash match
        if (parameters.ModelHash is not null)
        {
            model = currentModels.FirstOrDefault(m =>
                m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                && sha256.StartsWith(parameters.ModelHash, StringComparison.InvariantCultureIgnoreCase)
            );
        }
        else
        {
            // Name matches
            model = currentModels.FirstOrDefault(m => m.RelativePath.EndsWith(paramsModelName));
            model ??= currentModels.FirstOrDefault(m => m.ShortDisplayName.StartsWith(paramsModelName));
        }

        if (model is null)
            return;

        SelectedModel = model;
    }

    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256,
        };
    }
}

/// <summary>
/// Selects which empty-latent node the Wan text-to-video tab emits. See
/// <see cref="VideoModelCardViewModel.UseWan22FiveBLatent"/>.
/// </summary>
public enum WanLatentSourceMode
{
    /// <summary>Infer from the selected UNet name.</summary>
    Auto,

    /// <summary>Force the Wan 2.2 TI2V 5B latent (<c>Wan22ImageToVideoLatent</c>, 48-channel / 16x).</summary>
    Wan22FiveB,

    /// <summary>Force the Wan 2.1 / 14B latent (<c>EmptyHunyuanLatentVideo</c>, 16-channel / 8x).</summary>
    Legacy,
}
