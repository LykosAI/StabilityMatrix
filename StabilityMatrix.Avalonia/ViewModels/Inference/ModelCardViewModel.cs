using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
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
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Inference;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
[ManagedService]
[RegisterTransient<ModelCardViewModel>]
public partial class ModelCardViewModel(
    IInferenceClientManager clientManager,
    IServiceManager<ViewModelBase> vmFactory,
    TabContext tabContext,
    ISettingsManager settingsManager,
    IModelIndexService modelIndexService,
    INotificationService notificationService,
    ModelOrganizationService modelOrganizationService
) : LoadableViewModelBase, IParametersLoadableState, IComfyStep
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedUnifiedModel))]
    [NotifyPropertyChangedFor(nameof(WorkflowProfileStatusText))]
    [NotifyPropertyChangedFor(nameof(ShowWorkflowProfileStatus))]
    [NotifyPropertyChangedFor(nameof(RecommendedDefaultsToolTip))]
    [NotifyPropertyChangedFor(nameof(WorkflowProfileWarningText))]
    [NotifyPropertyChangedFor(nameof(ShowWorkflowProfileWarning))]
    [NotifyPropertyChangedFor(nameof(MoveModelToRecommendedFolderText))]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsGguf),
        nameof(ShowPrecisionSelection),
        nameof(SelectedUnifiedModel),
        nameof(HasActiveAdvancedOptions),
        nameof(AdvancedOptionsHeader),
        nameof(WorkflowProfileStatusText),
        nameof(ShowWorkflowProfileStatus),
        nameof(RecommendedDefaultsToolTip)
    )]
    private HybridModelFile? selectedUnetModel;

    /// <summary>
    /// Unified model property that auto-detects the loader type based on the model's SharedFolderType.
    /// Getter returns the currently active model based on IsStandaloneModelLoader.
    /// Setter auto-detects whether it's a checkpoint or UNet model and sets the appropriate properties.
    /// </summary>
    public HybridModelFile? SelectedUnifiedModel
    {
        get => IsStandaloneModelLoader ? SelectedUnetModel : SelectedModel;
        set
        {
            if (value is null)
            {
                // ComboBox selection can briefly report null while the model list refreshes.
                // Keep the active model so UNet-only encoder slots do not disappear transiently.
                return;
            }

            // Auto-detect model type based on folder
            if (value.Local?.SharedFolderType == SharedFolderType.DiffusionModels)
            {
                // It's a UNet model from diffusion_models folder
                SelectedModelLoader = ModelLoader.Unet;
                SelectedUnetModel = value;
            }
            else
            {
                // It's a checkpoint model
                SelectedModelLoader = ModelLoader.Default;
                SelectedModel = value;
            }
        }
    }

    [ObservableProperty]
    private bool isRefinerSelectionEnabled;

    [ObservableProperty]
    private bool showRefinerOption = true;

    [ObservableProperty]
    private HybridModelFile? selectedRefiner = HybridModelFile.None;

    [ObservableProperty]
    private HybridModelFile? selectedVae = HybridModelFile.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedOptions), nameof(AdvancedOptionsHeader))]
    private bool isVaeSelectionEnabled;

    [ObservableProperty]
    private bool disableSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAdvancedOptions), nameof(AdvancedOptionsHeader))]
    private bool isClipSkipEnabled;

    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Range(1, 24)]
    private int clipSkip = 1;

    [ObservableProperty]
    private bool isExtraNetworksEnabled;

    [ObservableProperty]
    private bool isModelLoaderSelectionEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsStandaloneModelLoader),
        nameof(SelectedUnifiedModel),
        nameof(ShowPrecisionSelection),
        nameof(ShowEncoderSection),
        nameof(HasActiveAdvancedOptions),
        nameof(AdvancedOptionsHeader),
        nameof(WorkflowProfileStatusText),
        nameof(ShowWorkflowProfileStatus),
        nameof(RecommendedDefaultsToolTip)
    )]
    private ModelLoader selectedModelLoader;

    /// <summary>
    /// Dynamic collection of text encoder slots.
    /// </summary>
    public ObservableCollection<TextEncoderSlotViewModel> TextEncoders { get; } = [];

    /// <summary>
    /// Whether the remove encoder button should be enabled.
    /// </summary>
    public bool CanRemoveEncoder => TextEncoders.Count > 1;

    /// <summary>
    /// Gets the selected model for encoder slot 1 (for backward compatibility with SetupClipLoaders).
    /// </summary>
    public HybridModelFile? SelectedClip1 => TextEncoders.Count > 0 ? TextEncoders[0].SelectedModel : null;

    /// <summary>
    /// Gets the selected model for encoder slot 2 (for backward compatibility with SetupClipLoaders).
    /// </summary>
    public HybridModelFile? SelectedClip2 => TextEncoders.Count > 1 ? TextEncoders[1].SelectedModel : null;

    /// <summary>
    /// Gets the selected model for encoder slot 3 (for backward compatibility with SetupClipLoaders).
    /// </summary>
    public HybridModelFile? SelectedClip3 => TextEncoders.Count > 2 ? TextEncoders[2].SelectedModel : null;

    /// <summary>
    /// Gets the selected model for encoder slot 4 (for backward compatibility with SetupClipLoaders).
    /// </summary>
    public HybridModelFile? SelectedClip4 => TextEncoders.Count > 3 ? TextEncoders[3].SelectedModel : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsSd3Clip),
        nameof(IsHiDreamClip),
        nameof(ResolvedWorkflowProfile),
        nameof(IsHiDreamWorkflow),
        nameof(IsZImageWorkflow),
        nameof(ShowShift),
        nameof(ShowEncoderTypeSelection),
        nameof(HasRecommendedDefaults),
        nameof(WorkflowProfileStatusText),
        nameof(ShowWorkflowProfileStatus),
        nameof(RecommendedDefaultsToolTip)
    )]
    private string? selectedClipType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ResolvedWorkflowProfile),
        nameof(IsHiDreamWorkflow),
        nameof(IsZImageWorkflow),
        nameof(ShowShift),
        nameof(ShowEncoderTypeSelection),
        nameof(HasRecommendedDefaults),
        nameof(WorkflowProfileStatusText),
        nameof(ShowWorkflowProfileStatus),
        nameof(RecommendedDefaultsToolTip)
    )]
    private InferenceWorkflowProfile selectedWorkflowProfile = InferenceWorkflowProfile.Auto;

    [ObservableProperty]
    private string? selectedDType;

    [ObservableProperty]
    private bool enableModelLoaderSelection = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEncoderSection))]
    private bool isClipModelSelectionEnabled;

    [ObservableProperty]
    private double shift = 3.0d;

    /// <summary>
    /// Whether the Advanced Options expander is expanded.
    /// </summary>
    [ObservableProperty]
    private bool isAdvancedOptionsExpanded;

    /// <summary>
    /// Whether the Text Encoders expander is expanded.
    /// </summary>
    [ObservableProperty]
    private bool isTextEncodersExpanded = true;

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e5m2"];
    public List<string> ClipTypes { get; set; } =
        ["flux", "flux2", "lumina2", "stable_diffusion", "sd3", "HiDream"];
    public List<InferenceWorkflowProfile> WorkflowProfiles { get; set; } =
        Enum.GetValues<InferenceWorkflowProfile>().ToList();

    public StackEditableCardViewModel ExtraNetworksStackCardViewModel { get; } =
        new(vmFactory) { Title = Resources.Label_ExtraNetworks, AvailableModules = [typeof(LoraModule)] };

    public IInferenceClientManager ClientManager { get; } = clientManager;

    public List<ModelLoader> ModelLoaders { get; } =
        Enum.GetValues<ModelLoader>().Except([ModelLoader.Gguf]).ToList();

    public bool IsStandaloneModelLoader => SelectedModelLoader is ModelLoader.Unet;
    public bool ShowPrecisionSelection => SelectedModelLoader is ModelLoader.Unet && !IsGguf;

    /// <summary>
    /// Whether to show the encoder section (only for UNet models when encoder selection is enabled).
    /// </summary>
    public bool ShowEncoderSection => IsClipModelSelectionEnabled && IsStandaloneModelLoader;

    public bool IsSd3Clip => SelectedClipType == "sd3";
    public bool IsHiDreamClip => SelectedClipType == "HiDream";
    public bool IsHiDreamWorkflow =>
        ResolvedWorkflowProfile is InferenceWorkflowProfile.HiDream
        || (ResolvedWorkflowProfile is InferenceWorkflowProfile.Custom && SelectedClipType == "HiDream");
    public bool IsZImageWorkflow =>
        ResolvedWorkflowProfile is InferenceWorkflowProfile.ZImageBase or InferenceWorkflowProfile.ZImageTurbo
        || (ResolvedWorkflowProfile is InferenceWorkflowProfile.Custom && SelectedClipType == "lumina2");
    public bool IsGguf => SelectedUnetModel?.RelativePath.EndsWith("gguf") ?? false;

    /// <summary>
    /// Whether any advanced options are currently visible (for expander header indication).
    /// Includes: Precision (UNet only), VAE, CLIP Skip.
    /// </summary>
    public bool HasActiveAdvancedOptions =>
        ShowPrecisionSelection || IsVaeSelectionEnabled || IsClipSkipEnabled;

    /// <summary>
    /// Header text for the Advanced Options expander, showing count of active options.
    /// </summary>
    public string AdvancedOptionsHeader
    {
        get
        {
            var count =
                (ShowPrecisionSelection ? 1 : 0)
                + (IsVaeSelectionEnabled ? 1 : 0)
                + (IsClipSkipEnabled ? 1 : 0);
            return count > 0 ? $"Advanced Options ({count})" : "Advanced Options";
        }
    }

    /// <summary>
    /// Header text for the Text Encoders expander, showing count of encoders.
    /// </summary>
    public string TextEncodersHeader => $"Text Encoders ({TextEncoders.Count})";

    /// <summary>
    /// Whether to show the Shift control (for HiDream and Z-Image workflows, only when in UNet mode).
    /// </summary>
    public bool ShowShift => ShowEncoderSection && (IsHiDreamWorkflow || IsZImageWorkflow);
    public bool ShowEncoderTypeSelection =>
        SelectedWorkflowProfile is InferenceWorkflowProfile.Custom
        || (ShowEncoderSection && SelectedWorkflowProfile is not InferenceWorkflowProfile.Auto);
    public InferenceWorkflowProfile ResolvedWorkflowProfile =>
        SelectedWorkflowProfile is InferenceWorkflowProfile.Auto
            ? InferWorkflowProfile()
            : SelectedWorkflowProfile;
    public bool HasRecommendedDefaults =>
        ResolvedWorkflowProfile
            is InferenceWorkflowProfile.DefaultCheckpoint
                or InferenceWorkflowProfile.Flux
                or InferenceWorkflowProfile.Flux2
                or InferenceWorkflowProfile.ZImageBase
                or InferenceWorkflowProfile.ZImageTurbo
                or InferenceWorkflowProfile.Anima;
    public bool ShowWorkflowProfileStatus =>
        SelectedWorkflowProfile is InferenceWorkflowProfile.Auto
        && SelectedUnifiedModel is not null
        && ResolvedWorkflowProfile is not InferenceWorkflowProfile.Custom;
    public string WorkflowProfileStatusText => $"Detected: {ResolvedWorkflowProfile.GetStringValue()}";
    public string RecommendedDefaultsToolTip =>
        ResolvedWorkflowProfile switch
        {
            InferenceWorkflowProfile.DefaultCheckpoint =>
                "Apply recommended sampler defaults: Euler Ancestral / Normal / 30 steps / CFG 5",
            InferenceWorkflowProfile.Flux =>
                "Apply recommended sampler defaults: Euler / Simple / 20 steps / CFG 3.5",
            InferenceWorkflowProfile.Flux2 =>
                "Apply recommended sampler defaults: Euler / Flux2Scheduler / 20 steps / CFG 5",
            InferenceWorkflowProfile.ZImageBase =>
                "Apply recommended sampler defaults: Res Multistep / Simple / 30 steps / CFG 4",
            InferenceWorkflowProfile.ZImageTurbo =>
                "Apply recommended sampler defaults: Res Multistep / Simple / 8 steps / CFG 1",
            InferenceWorkflowProfile.Anima =>
                "Apply recommended sampler defaults: ER SDE / Simple / 30 steps / CFG 4",
            _ => "No recommended sampler defaults for this workflow",
        };

    /// <summary>
    /// Describes a mismatch between the selected model's physical folder and the selected
    /// workflow. ComfyUI resolves each loader's paths relative to its own folder, so a
    /// checkpoint-folder file can never load through UNETLoader (and vice versa) — without
    /// this hint the user only finds out via a cryptic node-validation error at generation.
    /// <c>TargetFolder</c> is where the file should live if the profile is right.
    /// </summary>
    private (string Message, SharedFolderType TargetFolder)? GetProfileFolderMismatch()
    {
        if (SelectedUnifiedModel?.Local is not { } local)
            return null;

        var isStandaloneFile = local.SharedFolderType is SharedFolderType.DiffusionModels;

        // Explicit profile that disagrees with the file's folder. Auto follows the
        // folder and Custom is the user's business.
        if (SelectedWorkflowProfile is not (InferenceWorkflowProfile.Auto or InferenceWorkflowProfile.Custom))
        {
            // Flux.1 is packaging-flexible: users commonly have either a split UNet in
            // DiffusionModels or an all-in-one checkpoint in StableDiffusion.
            var profileWantsStandalone = SelectedWorkflowProfile switch
            {
                InferenceWorkflowProfile.DefaultCheckpoint => false,
                InferenceWorkflowProfile.Flux => (bool?)null,
                _ => true,
            };

            if (profileWantsStandalone is true && !isStandaloneFile)
            {
                return (
                    $"The {SelectedWorkflowProfile.GetStringValue()} workflow expects a model from "
                        + $"DiffusionModels, but this file is in {local.SharedFolderType}. It may not load correctly.",
                    SharedFolderType.DiffusionModels
                );
            }

            if (profileWantsStandalone is false && isStandaloneFile)
            {
                return (
                    "This file is in DiffusionModels and can't load as an all-in-one checkpoint. "
                        + "It may not load correctly.",
                    SharedFolderType.StableDiffusion
                );
            }
        }
        else if (SelectedWorkflowProfile is InferenceWorkflowProfile.Auto && !isStandaloneFile)
        {
            // Respect explicit metadata: an SDXL-tagged checkpoint that merely contains
            // "anima" or "flux2" in its filename is not a misplaced standalone model.
            var taggedBase = local.ConnectedModelInfo?.BaseModel;
            if (!string.IsNullOrWhiteSpace(taggedBase) && IsKnownCheckpointBaseModelTag(taggedBase))
                return null;

            // Checkpoint-folder file whose name/metadata implies a UNet-only architecture
            // (the classic "z-image in the StableDiffusion folder" support thread). Anima is
            // UNet-only too — it always pairs with a separate qwen_3_06b encoder + Qwen Image
            // VAE; the Civitai "Checkpoint" category on Anima models does not mean all-in-one.
            // Flux is excluded since Flux.1 all-in-one checkpoints are common and valid.
            var impliedProfile = InferWorkflowProfile(SelectedUnifiedModel, isUnetModel: true);
            if (
                impliedProfile
                is InferenceWorkflowProfile.Flux2
                    or InferenceWorkflowProfile.ZImageBase
                    or InferenceWorkflowProfile.ZImageTurbo
                    or InferenceWorkflowProfile.HiDream
                    or InferenceWorkflowProfile.Anima
            )
            {
                return (
                    $"This looks like {impliedProfile.GetStringValue()}, which loads from DiffusionModels, "
                        + $"but the file is in {local.SharedFolderType}. It may not load correctly.",
                    SharedFolderType.DiffusionModels
                );
            }
        }

        return null;
    }

    public string? WorkflowProfileWarningText => GetProfileFolderMismatch()?.Message;

    /// <summary>
    /// RelativePath of the model whose folder-mismatch warning the user dismissed.
    /// Selecting a different mismatched model shows the warning again; re-selecting the
    /// dismissed one stays quiet.
    /// </summary>
    private string? dismissedWarningModelPath;

    public bool ShowWorkflowProfileWarning =>
        WorkflowProfileWarningText is not null
        && !string.Equals(
            dismissedWarningModelPath,
            SelectedUnifiedModel?.Local?.RelativePath,
            StringComparison.OrdinalIgnoreCase
        );

    [RelayCommand]
    private void DismissWorkflowProfileWarning()
    {
        if (SelectedUnifiedModel?.Local?.RelativePath is { } path)
        {
            dismissedWarningModelPath = path;
        }

        OnPropertyChanged(nameof(ShowWorkflowProfileWarning));
    }

    /// <summary>
    /// Label for the one-click fix button next to the mismatch warning, e.g.
    /// "Move to DiffusionModels".
    /// </summary>
    public string? MoveModelToRecommendedFolderText =>
        GetProfileFolderMismatch() is { } mismatch
            ? $"Move to {mismatch.TargetFolder.GetStringValue()}"
            : null;

    /// <summary>
    /// Moves the selected model (and its metadata sidecars) into the folder the current
    /// workflow expects, refreshes the model index, and re-selects the moved file.
    /// </summary>
    [RelayCommand]
    private async Task MoveModelToRecommendedFolderAsync()
    {
        if (GetProfileFolderMismatch() is not { } mismatch || SelectedUnifiedModel?.Local is not { } local)
            return;

        var fileName = local.FileName;
        var destinationDir = Path.Combine(
            settingsManager.ModelsDirectory,
            mismatch.TargetFolder.GetStringValue()
        );

        try
        {
            await modelOrganizationService.MoveModelFileAsync(
                local,
                settingsManager.ModelsDirectory,
                destinationDir
            );
        }
        catch (FileTransferExistsException)
        {
            notificationService.Show(
                "Could not move model",
                $"A file named '{fileName}' already exists in the {mismatch.TargetFolder.GetStringValue()} folder.",
                NotificationType.Error
            );
            return;
        }
        catch (Exception e)
        {
            notificationService.Show(
                "Could not move model",
                $"[{e.GetType().Name}] {e.Message}",
                NotificationType.Error
            );
            return;
        }

        await modelIndexService.RefreshIndex();

        // The index-changed handler posts the refreshed model collections to the UI thread,
        // so re-select at Background priority to run after those updates have landed —
        // selecting before the item exists in the ComboBox items would reset it to null.
        Dispatcher.UIThread.Post(
            () =>
            {
                var movedModel = FindMovedModel(
                    mismatch.TargetFolder is SharedFolderType.DiffusionModels
                        ? ClientManager.UnetModels
                        : ClientManager.Models,
                    fileName
                );

                if (movedModel is not null)
                {
                    SelectedUnifiedModel = movedModel;
                }

                NotifyWorkflowProfileStateChanged();
            },
            DispatcherPriority.Background
        );

        notificationService.Show(
            "Model moved",
            $"Moved '{fileName}' to the {mismatch.TargetFolder.GetStringValue()} folder.",
            NotificationType.Success
        );
    }

    internal static HybridModelFile? FindMovedModel(
        IEnumerable<HybridModelFile> models,
        string destinationRelativePath
    ) =>
        models.FirstOrDefault(m =>
            m.Local != null
            && string.Equals(m.RelativePath, destinationRelativePath, StringComparison.OrdinalIgnoreCase)
        );

    public event Action<InferenceWorkflowProfile>? RecommendedDefaultsRequested;

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();
        ExtraNetworksStackCardViewModel.CardAdded += ExtraNetworksStackCardViewModelOnCardAdded;

        // Initialize default encoders if empty
        if (TextEncoders.Count == 0)
        {
            SetDefaultEncoderCount();
        }
    }

    public override void OnUnloaded()
    {
        base.OnUnloaded();
        ExtraNetworksStackCardViewModel.CardAdded -= ExtraNetworksStackCardViewModelOnCardAdded;
    }

    private void ExtraNetworksStackCardViewModelOnCardAdded(object? sender, LoadableViewModelBase e)
    {
        OnSelectedModelChanged(SelectedModel);
    }

    [RelayCommand]
    private static async Task OnConfigClickAsync()
    {
        await DialogHelper
            .CreateMarkdownDialog(
                """
                You can use a config (.yaml) file to load a model with specific settings.

                Place the config file next to the model file with the same name:
                ```md
                Models/
                    StableDiffusion/
                        my_model.safetensors
                        my_model.yaml <-
                ```
                """,
                "Using Model Configs",
                TextEditorPreset.Console
            )
            .ShowAsync();
    }

    [RelayCommand]
    private async Task OpenModelPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Model";
        pickerVm.PreferredWorkflowProfile = SelectedWorkflowProfile;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                // Auto-detect model type based on folder
                if (selected.Local?.SharedFolderType == SharedFolderType.DiffusionModels)
                {
                    // It's a UNet model from diffusion_models folder
                    SelectedModelLoader = ModelLoader.Unet;
                    SelectedUnetModel = selected;
                }
                else
                {
                    // It's a checkpoint model
                    SelectedModelLoader = ModelLoader.Default;
                    SelectedModel = selected;
                }
            }
        }
    }

    [RelayCommand]
    private async Task OpenRefinerPickerAsync()
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = "Select Refiner";
        pickerVm.Source = ModelPickerSource.CheckpointAndUnet;
        pickerVm.ShowCheckpointsOnly = true;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                SelectedRefiner = selected;
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
    private async Task OpenClipPickerAsync(TextEncoderSlotViewModel encoderSlot)
    {
        using var pickerScope = vmFactory.CreateScope();
        var pickerVm = pickerScope.ServiceManager.Get<ModelPickerDialogViewModel>();
        pickerVm.Title = $"Select Text Encoder ({encoderSlot.Label})";
        pickerVm.Source = ModelPickerSource.Clip;

        if (await pickerVm.GetDialog().ShowAsync() == ContentDialogResult.Primary)
        {
            if (pickerVm.SelectedModel is { } selected)
            {
                encoderSlot.SelectedModel = selected;
            }
        }
    }

    public async Task<bool> ValidateModel()
    {
        if (IsStandaloneModelLoader && SelectedUnetModel != null)
            return true;

        if (!IsStandaloneModelLoader && SelectedModel != null)
            return true;

        var dialog = DialogHelper.CreateMarkdownDialog(
            "Please select a model to continue.",
            "No Model Selected"
        );
        await dialog.ShowAsync();
        return false;
    }

    private static ComfyTypedNodeBase<
        ModelNodeConnection,
        ClipNodeConnection,
        VAENodeConnection
    > GetDefaultModelLoader(ModuleApplyStepEventArgs e, string nodeName, HybridModelFile model)
    {
        // Check if config
        if (model.Local?.ConfigFullPath is { } configPath)
        {
            // We'll need to upload the config file to `models/configs` later
            var uploadConfigPath = e.AddFileTransferToConfigs(configPath);

            return new ComfyNodeBuilder.CheckpointLoader
            {
                Name = nodeName,
                // Only the file name is needed
                ConfigName = Path.GetFileName(uploadConfigPath),
                CkptName = model.RelativePath,
            };
        }

        // Simple loader if no config
        return new ComfyNodeBuilder.CheckpointLoaderSimple { Name = nodeName, CkptName = model.RelativePath };
    }

    /// <inheritdoc />
    public virtual void ApplyStep(ModuleApplyStepEventArgs e)
    {
        if (SelectedModelLoader is ModelLoader.Default or ModelLoader.Nf4)
        {
            SetupDefaultModelLoader(e);
        }
        else // UNET/GGUF UNET workflow
        {
            SetupStandaloneModelLoader(e);
        }

        // Clip skip all models if enabled
        if (IsClipSkipEnabled)
        {
            foreach (var (modelName, model) in e.Builder.Connections.Models)
            {
                if (model.Clip is not { } modelClip)
                    continue;

                var clipSetLastLayer = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.CLIPSetLastLayer
                    {
                        Name = $"CLIP_Skip_{modelName}",
                        Clip = modelClip,
                        // Need to convert to negative indexing from (1 to 24) to (-1 to -24)
                        StopAtClipLayer = -ClipSkip,
                    }
                );

                model.Clip = clipSetLastLayer.Output;
            }
        }

        // Load extra networks if enabled
        if (IsExtraNetworksEnabled)
        {
            ExtraNetworksStackCardViewModel.ApplyStep(e);
        }
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        // Build encoder names list from dynamic collection
        var encoderNames = TextEncoders.Select(e => e.SelectedModel?.RelativePath).ToList();

        return SerializeModel(
            new ModelCardModel
            {
                SelectedModelName = IsStandaloneModelLoader
                    ? SelectedUnetModel?.RelativePath
                    : SelectedModel?.RelativePath,
                SelectedVaeName = SelectedVae?.RelativePath,
                SelectedRefinerName = SelectedRefiner?.RelativePath,
                ClipSkip = ClipSkip,
                IsVaeSelectionEnabled = IsVaeSelectionEnabled,
                IsRefinerSelectionEnabled = IsRefinerSelectionEnabled,
                IsClipSkipEnabled = IsClipSkipEnabled,
                IsExtraNetworksEnabled = IsExtraNetworksEnabled,
                IsModelLoaderSelectionEnabled = IsModelLoaderSelectionEnabled,
                // For backward compatibility, also save to legacy fields
                SelectedClip1Name = encoderNames.ElementAtOrDefault(0),
                SelectedClip2Name = encoderNames.ElementAtOrDefault(1),
                SelectedClip3Name = encoderNames.ElementAtOrDefault(2),
                SelectedClip4Name = encoderNames.ElementAtOrDefault(3),
                // New field for dynamic encoders (for future proofing if > 4 encoders)
                TextEncoderNames = encoderNames,
                SelectedClipType = SelectedClipType,
                SelectedWorkflowProfile = SelectedWorkflowProfile,
                SelectedDType = SelectedDType,
                Shift = Shift,
                IsClipModelSelectionEnabled = IsClipModelSelectionEnabled,
                ModelLoader = SelectedModelLoader,
                ShowRefinerOption = ShowRefinerOption,
                IsAdvancedOptionsExpanded = IsAdvancedOptionsExpanded,
                IsTextEncodersExpanded = IsTextEncodersExpanded,
                ExtraNetworks = ExtraNetworksStackCardViewModel.SaveStateToJsonObject(),
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ModelCardModel>(state);

        // Set loading flag to prevent auto-adjustment of encoder count
        isLoadingState = true;

        try
        {
            // uwu 123
            // :thinknom:
            // :thinkcode:
            SelectedModelLoader =
                model.ModelLoader is ModelLoader.Gguf ? ModelLoader.Unet : model.ModelLoader;

            if (SelectedModelLoader is ModelLoader.Unet)
            {
                SelectedUnetModel = model.SelectedModelName is null
                    ? null
                    : ClientManager.UnetModels.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);
            }
            else
            {
                SelectedModel = model.SelectedModelName is null
                    ? null
                    : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);
            }

            SelectedVae = model.SelectedVaeName is null
                ? HybridModelFile.Default
                : ClientManager.VaeModels.FirstOrDefault(x => x.RelativePath == model.SelectedVaeName);

            SelectedRefiner = model.SelectedRefinerName is null
                ? HybridModelFile.None
                : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedRefinerName);

            // Load encoder type first (needed for default encoder count)
            SelectedClipType = model.SelectedClipType;
            SelectedWorkflowProfile = model.SelectedWorkflowProfile;

            // Load text encoders from saved state
            LoadTextEncodersFromModel(model);

            SelectedDType = model.SelectedDType;
            Shift = model.Shift;
            ClipSkip = model.ClipSkip;

            IsVaeSelectionEnabled = model.IsVaeSelectionEnabled;
            IsRefinerSelectionEnabled = model.IsRefinerSelectionEnabled;
            ShowRefinerOption = model.ShowRefinerOption;
            IsClipSkipEnabled = model.IsClipSkipEnabled;
            IsExtraNetworksEnabled = model.IsExtraNetworksEnabled;
            IsModelLoaderSelectionEnabled = model.IsModelLoaderSelectionEnabled;
            IsClipModelSelectionEnabled = model.IsClipModelSelectionEnabled;
            IsAdvancedOptionsExpanded = model.IsAdvancedOptionsExpanded;
            IsTextEncodersExpanded = model.IsTextEncodersExpanded;

            if (model.ExtraNetworks is not null)
            {
                ExtraNetworksStackCardViewModel.LoadStateFromJsonObject(model.ExtraNetworks);
            }
        }
        finally
        {
            isLoadingState = false;
            NotifyWorkflowProfileStateChanged();
        }
    }

    private InferenceWorkflowProfile InferWorkflowProfile()
    {
        return InferWorkflowProfile(
            SelectedUnifiedModel,
            SelectedModelLoader is ModelLoader.Unet
                || SelectedUnifiedModel?.Local?.SharedFolderType is SharedFolderType.DiffusionModels
        );
    }

    private static InferenceWorkflowProfile InferWorkflowProfile(HybridModelFile? model, bool isUnetModel)
    {
        if (!isUnetModel)
            return InferenceWorkflowProfile.DefaultCheckpoint;

        var baseModel = model?.Local?.ConnectedModelInfo?.BaseModel;

        if (!string.IsNullOrWhiteSpace(baseModel))
        {
            if (baseModel.Equals("ZImageTurbo", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.ZImageTurbo;

            if (baseModel.Equals("ZImageBase", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.ZImageBase;

            if (baseModel.Equals("Anima", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.Anima;

            if (baseModel.StartsWith("Flux.2", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.Flux2;

            if (baseModel.StartsWith("Flux.1", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.Flux;

            if (baseModel.Equals("HiDream", StringComparison.OrdinalIgnoreCase))
                return InferenceWorkflowProfile.HiDream;
        }

        var name = model?.RelativePath ?? string.Empty;

        if (
            name.Contains("z_image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("z-image", StringComparison.OrdinalIgnoreCase)
            || name.Contains("zimage", StringComparison.OrdinalIgnoreCase)
        )
        {
            return name.Contains("turbo", StringComparison.OrdinalIgnoreCase)
                ? InferenceWorkflowProfile.ZImageTurbo
                : InferenceWorkflowProfile.ZImageBase;
        }

        if (
            name.Contains("flux2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("flux-2", StringComparison.OrdinalIgnoreCase)
            || name.Contains("flux_2", StringComparison.OrdinalIgnoreCase)
        )
            return InferenceWorkflowProfile.Flux2;

        if (name.Contains("flux", StringComparison.OrdinalIgnoreCase))
            return InferenceWorkflowProfile.Flux;

        if (IsAnimaName(name))
            return InferenceWorkflowProfile.Anima;

        if (name.Contains("hidream", StringComparison.OrdinalIgnoreCase))
            return InferenceWorkflowProfile.HiDream;

        return InferenceWorkflowProfile.DefaultCheckpoint;
    }

    /// <summary>
    /// Matches "anima" only as its own token — "animagine" / "animaPencil" are SDXL models
    /// that must not be detected as the Anima architecture.
    /// </summary>
    private static bool IsAnimaName(string name) => AnimaNameRegex().IsMatch(name);

    /// <summary>
    /// Whether a CivitAI BaseModel tag is specific enough to override filename heuristics for
    /// a checkpoint-folder file. "Other" remains inconclusive, while known UNet-only tags are
    /// allowed to proceed to the misplaced-model check.
    /// </summary>
    private static bool IsKnownCheckpointBaseModelTag(string baseModel) =>
        Enum.GetValues<CivitBaseModelType>()
            .Any(type =>
                type is not (CivitBaseModelType.Other or CivitBaseModelType.HiDream)
                && type.GetStringValue().Equals(baseModel, StringComparison.OrdinalIgnoreCase)
            );

    [GeneratedRegex(@"(?<![\p{L}])anima(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex AnimaNameRegex();

    /// <summary>
    /// Loads text encoders from the saved model state, supporting both new and legacy formats.
    /// </summary>
    private void LoadTextEncodersFromModel(ModelCardModel model)
    {
        TextEncoders.Clear();

        // Try new format first (TextEncoderNames list)
        if (model.TextEncoderNames is { Count: > 0 })
        {
            for (var i = 0; i < model.TextEncoderNames.Count; i++)
            {
                var slot = new TextEncoderSlotViewModel(i + 1);
                var encoderName = model.TextEncoderNames[i];
                if (encoderName is not null)
                {
                    slot.SelectedModel = ClientManager.ClipModels.FirstOrDefault(x =>
                        x.RelativePath == encoderName
                    );
                }
                TextEncoders.Add(slot);
            }
        }
        else
        {
            // Fall back to legacy format (SelectedClip1-4)
            var legacyNames = new[]
            {
                model.SelectedClip1Name,
                model.SelectedClip2Name,
                model.SelectedClip3Name,
                model.SelectedClip4Name,
            };

            // Count how many legacy encoders were set (non-null)
            var encoderCount = legacyNames.TakeWhile(n => n is not null).Count();

            // Use at least the default count for the encoder type
            var defaultCount = SelectedClipType switch
            {
                "flux" => 2,
                "flux2" or "lumina2" or "stable_diffusion" => 1,
                "sd3" => 3,
                "HiDream" => 4,
                _ => 2,
            };
            encoderCount = Math.Max(encoderCount, defaultCount);

            for (var i = 0; i < encoderCount; i++)
            {
                var slot = new TextEncoderSlotViewModel(i + 1);
                var encoderName = legacyNames.ElementAtOrDefault(i);
                if (encoderName is not null)
                {
                    slot.SelectedModel = ClientManager.ClipModels.FirstOrDefault(x =>
                        x.RelativePath == encoderName
                    );
                }
                TextEncoders.Add(slot);
            }
        }

        OnPropertyChanged(nameof(CanRemoveEncoder));
        OnPropertyChanged(nameof(TextEncodersHeader));
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.Models.Concat(ClientManager.UnetModels).ToList();
        var currentExtraNetworks = ClientManager.LoraModels.ToList();

        HybridModelFile? model;

        // First try hash match
        if (parameters.ModelHash is not null)
        {
            model = currentModels.FirstOrDefault(m =>
                m.Local?.ConnectedModelInfo?.Hashes.SHA256 is { } sha256
                && sha256.StartsWith(parameters.ModelHash, StringComparison.InvariantCultureIgnoreCase)
            );
        }
        else if (parameters.ModelVersionId is not null)
        {
            model = currentModels.FirstOrDefault(m =>
                m.Local?.ConnectedModelInfo?.VersionId == parameters.ModelVersionId
            );
        }
        else
        {
            // Name matches
            model = currentModels.FirstOrDefault(m => m.RelativePath.EndsWith(paramsModelName));
            model ??= currentModels.FirstOrDefault(m => m.ShortDisplayName.StartsWith(paramsModelName));
        }

        ExtraNetworksStackCardViewModel.Clear();

        if (parameters.ExtraNetworkModelVersionIds is not null)
        {
            IsExtraNetworksEnabled = true;

            foreach (var versionId in parameters.ExtraNetworkModelVersionIds)
            {
                var module = ExtraNetworksStackCardViewModel.AddModule<LoraModule>();
                module.GetCard<ExtraNetworkCardViewModel>().SelectedModel =
                    currentExtraNetworks.FirstOrDefault(m =>
                        m.Local?.ConnectedModelInfo?.VersionId == versionId
                    );
                module.IsEnabled = true;
            }
        }

        if (model is null)
            return;

        if (model.Local?.SharedFolderType is SharedFolderType.DiffusionModels)
        {
            SelectedModelLoader = ModelLoader.Unet;
            SelectedUnetModel = model;
        }
        else
        {
            SelectedModelLoader = ModelLoader.Default;
            SelectedModel = model;
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        if (IsStandaloneModelLoader)
        {
            return parameters with
            {
                ModelName = SelectedUnetModel?.FileName,
                ModelHash = SelectedUnetModel?.Local?.ConnectedModelInfo?.Hashes.SHA256,
            };
        }

        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256,
        };
    }

    partial void OnSelectedModelLoaderChanged(ModelLoader value)
    {
        if (value is ModelLoader.Unet)
        {
            if (!IsVaeSelectionEnabled)
                IsVaeSelectionEnabled = true;

            if (!IsClipModelSelectionEnabled)
                IsClipModelSelectionEnabled = true;

            if (TextEncoders.Count == 0)
                SetDefaultEncoderCount();
        }
        else if (value is ModelLoader.Default or ModelLoader.Nf4 && !isLoadingState)
        {
            // The separate text-encoder UI is only reachable for standalone loaders, so a
            // stale true here (left over from a previously selected UNet model) makes
            // generation demand encoders the user can't even see. Also drop any encoder /
            // VAE values WE auto-filled for the previous UNet workflow — a Flux.2 VAE
            // override on an all-in-one checkpoint would just corrupt its output.
            IsClipModelSelectionEnabled = false;
            ClearAutoSelectedComponents();
        }

        RefreshWorkflowProfileState();
    }

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        // Update TabContext with the selected model
        tabContext.SelectedModel = value;

        if (!IsExtraNetworksEnabled)
            return;

        foreach (var card in ExtraNetworksStackCardViewModel.Cards)
        {
            if (card is not LoraModule loraModule)
                continue;

            if (loraModule.GetCard<ExtraNetworkCardViewModel>() is not { } cardViewModel)
                continue;

            cardViewModel.SelectedBaseModel = value;
        }
    }

    partial void OnSelectedUnetModelChanged(HybridModelFile? value)
    {
        OnSelectedModelChanged(value);
        RefreshWorkflowProfileState();
    }

    partial void OnSelectedClipTypeChanged(string? value)
    {
        // When encoder type changes, set the default encoder count for that type
        // But only if we're not loading state (to preserve user's custom encoder count)
        if (!isLoadingState)
        {
            SetDefaultEncoderCount(preserveUserSelections: true);
        }
    }

    partial void OnSelectedWorkflowProfileChanged(InferenceWorkflowProfile value)
    {
        if (!isLoadingState)
        {
            SyncModelLoaderToProfile(value);
            ApplyDefaultClipTypeForResolvedProfile(preserveUserSelections: true);
        }

        RefreshWorkflowProfileState();
    }

    /// <summary>
    /// Keeps <see cref="SelectedModelLoader"/> in sync with the user-facing Workflow dropdown.
    /// The encoder / precision / text-encoder fields (and the generated workflow itself) key off
    /// the model loader, so without this, picking "Default / Checkpoint" on a model that was
    /// auto-detected as a UNet workflow leaves those separate-component fields visible and still
    /// required at generation time. All-in-one checkpoints bundle their own VAE + text encoder,
    /// so switching to one also drops the VAE / text-encoder selectors the UNet profile turned on.
    /// </summary>
    private void SyncModelLoaderToProfile(InferenceWorkflowProfile profile)
    {
        // Whether the profile uses separate diffusion-model + encoder + VAE files (true) or an
        // all-in-one checkpoint (false). Null = leave the loader alone (Custom, or Auto with
        // no model yet to infer from).
        var wantStandalone = profile switch
        {
            InferenceWorkflowProfile.DefaultCheckpoint => false,
            InferenceWorkflowProfile.Flux
            or InferenceWorkflowProfile.Flux2
            or InferenceWorkflowProfile.Anima
            or InferenceWorkflowProfile.ZImageBase
            or InferenceWorkflowProfile.ZImageTurbo
            or InferenceWorkflowProfile.HiDream => true,
            // Auto follows the selected model's folder (diffusion_models -> UNet); Custom is
            // left entirely to the user.
            InferenceWorkflowProfile.Auto when SelectedUnifiedModel is not null => SelectedUnifiedModel
                .Local
                ?.SharedFolderType is SharedFolderType.DiffusionModels,
            _ => (bool?)null,
        };

        if (wantStandalone is not { } standalone)
            return;

        var currentlyStandalone = SelectedModelLoader is ModelLoader.Unet or ModelLoader.Gguf;
        var currentModel = SelectedUnifiedModel;

        // A local file can only be loaded by the loader matching its physical folder — ComfyUI
        // resolves CheckpointLoader paths against the checkpoints folder and UNETLoader paths
        // against diffusion_models, so carrying a model across to the other loader is a
        // guaranteed node-validation error. When the profile disagrees with the file's folder,
        // keep the current loader and let WorkflowProfileWarningText explain the mismatch.
        var canSwitchLoader =
            currentModel?.Local is not { } local
            || (local.SharedFolderType is SharedFolderType.DiffusionModels) == standalone;

        // The loader this card will actually end on after this call.
        var effectiveStandalone = canSwitchLoader ? standalone : currentlyStandalone;

        // Reconcile the encoder-selection flag with that loader IN BOTH DIRECTIONS, even when
        // no loader flip happens below. The encoder UI only exists for standalone loaders, so
        // a mismatched flag strands the user either way: stuck true on a checkpoint makes
        // generation demand encoders the UI doesn't show, and stuck false on a UNet loader
        // hides the encoder slots with no profile toggle able to bring them back.
        if (effectiveStandalone)
        {
            if (!IsClipModelSelectionEnabled)
                IsClipModelSelectionEnabled = true;

            if (!IsVaeSelectionEnabled)
                IsVaeSelectionEnabled = true;

            if (TextEncoders.Count == 0)
                SetDefaultEncoderCount();
        }
        else if (IsClipModelSelectionEnabled)
        {
            IsClipModelSelectionEnabled = false;
        }

        // Only flip between the checkpoint-style (Default/Nf4) and UNet-style (Unet/Gguf) groups
        // when they actually disagree — this preserves Nf4/Gguf nuances when already on the
        // correct side.
        if (!canSwitchLoader || standalone == currentlyStandalone)
            return;

        // The two loaders read different backing fields (SelectedUnetModel vs SelectedModel), so
        // carry the current selection across so the model picker doesn't blank out on the switch.
        if (standalone)
        {
            SelectedModelLoader = ModelLoader.Unet;
            SelectedUnetModel = currentModel;
        }
        else
        {
            IsVaeSelectionEnabled = false;
            SelectedModelLoader = ModelLoader.Default;
            SelectedModel = currentModel;
        }
    }

    private void NotifyWorkflowProfileStateChanged()
    {
        OnPropertyChanged(nameof(ResolvedWorkflowProfile));
        OnPropertyChanged(nameof(IsHiDreamWorkflow));
        OnPropertyChanged(nameof(IsZImageWorkflow));
        OnPropertyChanged(nameof(ShowShift));
        OnPropertyChanged(nameof(ShowEncoderTypeSelection));
        OnPropertyChanged(nameof(HasRecommendedDefaults));
        OnPropertyChanged(nameof(WorkflowProfileStatusText));
        OnPropertyChanged(nameof(ShowWorkflowProfileStatus));
        OnPropertyChanged(nameof(RecommendedDefaultsToolTip));
        OnPropertyChanged(nameof(WorkflowProfileWarningText));
        OnPropertyChanged(nameof(ShowWorkflowProfileWarning));
        OnPropertyChanged(nameof(MoveModelToRecommendedFolderText));
    }

    private void RefreshWorkflowProfileState()
    {
        NotifyWorkflowProfileStateChanged();

        if (!isLoadingState)
        {
            ApplyDefaultClipTypeForResolvedProfile(preserveUserSelections: true);
            AutoSelectComponentsForResolvedProfile();
        }
    }

    private void ApplyDefaultClipTypeForResolvedProfile(bool preserveUserSelections)
    {
        if (SelectedWorkflowProfile is InferenceWorkflowProfile.Custom)
            return;

        var clipType = ResolvedWorkflowProfile switch
        {
            InferenceWorkflowProfile.Flux => "flux",
            InferenceWorkflowProfile.Flux2 => "flux2",
            InferenceWorkflowProfile.ZImageBase or InferenceWorkflowProfile.ZImageTurbo => "lumina2",
            InferenceWorkflowProfile.Anima => "stable_diffusion",
            InferenceWorkflowProfile.HiDream => "HiDream",
            _ => SelectedClipType,
        };

        if (string.IsNullOrWhiteSpace(clipType) || SelectedClipType == clipType)
            return;

        // The resolved profile genuinely changed. Selections WE made for the previous
        // profile are defaults, not user choices — drop them so the slot count can resize
        // and the new profile's defaults can fill in. Manual picks are left untouched.
        ClearAutoSelectedComponents();

        SelectedClipType = clipType;
        SetDefaultEncoderCount(preserveUserSelections);
    }

    /// <summary>
    /// Flag to prevent auto-adjustment during state loading.
    /// </summary>
    private bool isLoadingState;

    /// <summary>
    /// RelativePaths of encoder / VAE selections this card filled in automatically. When the
    /// resolved profile or model changes, selections still in this set are treated as
    /// replaceable defaults; anything else is a user choice and is never touched.
    /// </summary>
    private readonly HashSet<string> autoSelectedComponentPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fills empty text-encoder slots and the default VAE with known-compatible local files
    /// for the resolved workflow profile (e.g. qwen_3_4b for Flux.2 Klein 4B, clip_l + t5xxl
    /// for Flux.1). Saves users from having to know which encoder file pairs with which
    /// model architecture — the top "where has the text encoder gone" support question.
    /// </summary>
    private void AutoSelectComponentsForResolvedProfile()
    {
        if (isLoadingState || !IsStandaloneModelLoader || SelectedUnetModel is null)
            return;

        var profile = ResolvedWorkflowProfile;

        AutoSelectVae(profile);

        var matchers = GetDefaultEncoderMatchersForProfile(profile);
        for (var i = 0; i < TextEncoders.Count && i < matchers.Count; i++)
        {
            AutoSelectEncoderSlot(TextEncoders[i], matchers[i]);
        }
    }

    private void AutoSelectVae(InferenceWorkflowProfile profile)
    {
        var current = SelectedVae;
        var isEmpty = current is null || current.IsDefault || current.IsNone;
        var isAutoSelected =
            !isEmpty && current!.RelativePath is { } path && autoSelectedComponentPaths.Contains(path);

        if (!isEmpty && !isAutoSelected)
            return; // explicit user pick — never override

        if (isAutoSelected && MatchesDefaultVaeForProfile(profile, current!.FileName))
            return; // our earlier pick is still correct for this profile

        var vae = ClientManager.VaeModels.FirstOrDefault(m =>
            m.Local != null && MatchesDefaultVaeForProfile(profile, m.FileName)
        );

        if (vae is null)
            return;

        if (isAutoSelected)
            autoSelectedComponentPaths.Remove(current!.RelativePath);

        SelectedVae = vae;
        autoSelectedComponentPaths.Add(vae.RelativePath);
    }

    private void AutoSelectEncoderSlot(TextEncoderSlotViewModel slot, Func<string, bool> matcher)
    {
        var current = slot.SelectedModel;
        var isEmpty = current is null || current.IsNone;
        var isAutoSelected =
            !isEmpty && current!.RelativePath is { } path && autoSelectedComponentPaths.Contains(path);

        if (!isEmpty && !isAutoSelected)
            return; // explicit user pick — never override

        if (isAutoSelected && matcher(current!.FileName))
            return; // our earlier pick is still correct (e.g. same encoder across profiles)

        var encoder = ClientManager.ClipModels.FirstOrDefault(m => m.Local != null && matcher(m.FileName));

        if (encoder is null)
            return;

        if (isAutoSelected)
            autoSelectedComponentPaths.Remove(current!.RelativePath);

        slot.SelectedModel = encoder;
        autoSelectedComponentPaths.Add(encoder.RelativePath);
    }

    /// <summary>
    /// Clears encoder / VAE selections that were auto-filled (leaving user picks alone) so a
    /// profile switch can resize the slots and re-derive defaults for the new profile.
    /// </summary>
    private void ClearAutoSelectedComponents()
    {
        if (autoSelectedComponentPaths.Count == 0)
            return;

        foreach (var slot in TextEncoders)
        {
            if (
                slot.SelectedModel is { IsNone: false, IsDefault: false } model
                && autoSelectedComponentPaths.Contains(model.RelativePath)
            )
            {
                // The slot setter ignores null (transient ComboBox refresh guard), so clear
                // with the None sentinel which the empty-slot checks already understand.
                slot.SelectedModel = HybridModelFile.None;
            }
        }

        if (
            SelectedVae is { IsNone: false, IsDefault: false } vae
            && autoSelectedComponentPaths.Contains(vae.RelativePath)
        )
        {
            SelectedVae = HybridModelFile.Default;
        }

        autoSelectedComponentPaths.Clear();
    }

    /// <summary>
    /// Per-slot filename matchers for the encoder files each profile expects, in slot order.
    /// Empty for profiles where the encoder is baked into the checkpoint or unknown.
    /// </summary>
    private IReadOnlyList<Func<string, bool>> GetDefaultEncoderMatchersForProfile(
        InferenceWorkflowProfile profile
    )
    {
        switch (profile)
        {
            case InferenceWorkflowProfile.Flux2:
                // Klein 4B pairs with qwen_3_4b, Klein 9B with qwen_3_8b — picking the wrong
                // size fails at sampling with a tensor-shape mismatch.
                var encoderSize = SelectedUnetModel is { } unet
                    ? Flux2KleinModelManager.GetExpectedEncoderSize(unet)
                    : "4b";
                return
                [
                    name =>
                        Flux2KleinModelManager.IsKleinTextEncoder(name)
                        && Flux2KleinModelManager.MatchesEncoderSize(name, encoderSize),
                ];
            case InferenceWorkflowProfile.Flux:
                return
                [
                    name => name.Contains("clip_l", StringComparison.OrdinalIgnoreCase),
                    name =>
                        name.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase),
                ];
            case InferenceWorkflowProfile.ZImageBase:
            case InferenceWorkflowProfile.ZImageTurbo:
                // Z-Image uses the Qwen3 4B encoder (the same qwen_3_4b.safetensors file
                // Flux.2 Klein 4B uses).
                return
                [
                    name =>
                        Flux2KleinModelManager.IsKleinTextEncoder(name)
                        && Flux2KleinModelManager.MatchesEncoderSize(name, "4b"),
                ];
            case InferenceWorkflowProfile.Anima:
                // Official Anima encoder is qwen_3_06b_base.safetensors (Qwen3 0.6B).
                return
                [
                    name =>
                        name.Contains("qwen_3_06b", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("qwen3_06b", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("qwen_3_0.6b", StringComparison.OrdinalIgnoreCase),
                ];
            case InferenceWorkflowProfile.HiDream:
                return
                [
                    name => name.Contains("clip_l", StringComparison.OrdinalIgnoreCase),
                    name => name.Contains("clip_g", StringComparison.OrdinalIgnoreCase),
                    name =>
                        name.Contains("t5xxl", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("t5-xxl", StringComparison.OrdinalIgnoreCase),
                    name => name.Contains("llama", StringComparison.OrdinalIgnoreCase),
                ];
            default:
                return [];
        }
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> is the known-default VAE for the profile.
    /// Flux.1, Z-Image and HiDream all use the Flux.1 VAE (ae.safetensors); Flux.2 has its
    /// own VAE; Anima reuses the Qwen Image VAE.
    /// </summary>
    private static bool MatchesDefaultVaeForProfile(InferenceWorkflowProfile profile, string fileName) =>
        profile switch
        {
            InferenceWorkflowProfile.Flux2 => Flux2KleinModelManager.IsFlux2Vae(fileName),
            InferenceWorkflowProfile.Flux
            or InferenceWorkflowProfile.ZImageBase
            or InferenceWorkflowProfile.ZImageTurbo
            or InferenceWorkflowProfile.HiDream => fileName.Equals(
                "ae.safetensors",
                StringComparison.OrdinalIgnoreCase
            ),
            InferenceWorkflowProfile.Anima => fileName.Contains(
                "qwen_image_vae",
                StringComparison.OrdinalIgnoreCase
            ),
            _ => false,
        };

    /// <summary>
    /// Sets the default number of encoder slots based on the selected clip type.
    /// </summary>
    /// <param name="preserveUserSelections">If true, only adjust if no encoders have been configured yet.</param>
    private void SetDefaultEncoderCount(bool preserveUserSelections = false)
    {
        // If preserving user selections and any encoder has a model selected, skip adjustment
        if (preserveUserSelections && TextEncoders.Any(e => e.SelectedModel is { IsNone: false }))
        {
            return;
        }

        var targetCount = SelectedClipType switch
        {
            "flux" => 2,
            "flux2" or "lumina2" or "stable_diffusion" => 1,
            "sd3" => 3,
            "HiDream" => 4,
            _ => 2, // Default to 2 for unknown types
        };

        // Add or remove encoders to match target count
        while (TextEncoders.Count < targetCount)
        {
            TextEncoders.Add(new TextEncoderSlotViewModel(TextEncoders.Count + 1));
        }

        while (TextEncoders.Count > targetCount)
        {
            TextEncoders.RemoveAt(TextEncoders.Count - 1);
        }

        OnPropertyChanged(nameof(CanRemoveEncoder));
        OnPropertyChanged(nameof(TextEncodersHeader));
    }

    /// <summary>
    /// Adds a new text encoder slot.
    /// </summary>
    [RelayCommand]
    private void AddEncoder()
    {
        TextEncoders.Add(new TextEncoderSlotViewModel(TextEncoders.Count + 1));
        OnPropertyChanged(nameof(CanRemoveEncoder));
        OnPropertyChanged(nameof(TextEncodersHeader));
    }

    /// <summary>
    /// Removes the last text encoder slot.
    /// </summary>
    [RelayCommand]
    private void RemoveEncoder()
    {
        if (TextEncoders.Count > 1)
        {
            TextEncoders.RemoveAt(TextEncoders.Count - 1);
            OnPropertyChanged(nameof(CanRemoveEncoder));
            OnPropertyChanged(nameof(TextEncodersHeader));
        }
    }

    [RelayCommand]
    private void ApplyRecommendedDefaults()
    {
        if (!HasRecommendedDefaults)
            return;

        RecommendedDefaultsRequested?.Invoke(ResolvedWorkflowProfile);
    }

    private void SetupStandaloneModelLoader(ModuleApplyStepEventArgs e)
    {
        if (SelectedModelLoader is ModelLoader.Unet && IsGguf)
        {
            var checkpointLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.UnetLoaderGGUF
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                    UnetName =
                        SelectedUnetModel?.RelativePath
                        ?? throw new ValidationException("Model not selected"),
                }
            );
            e.Builder.Connections.Base.Model = checkpointLoader.Output;
        }
        else
        {
            var checkpointLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.UNETLoader
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                    UnetName =
                        SelectedUnetModel?.RelativePath
                        ?? throw new ValidationException("Model not selected"),
                    WeightDtype = SelectedDType ?? "default",
                }
            );
            e.Builder.Connections.Base.Model = checkpointLoader.Output;
        }

        if (SelectedModelLoader is ModelLoader.Unet && IsHiDreamWorkflow)
        {
            var modelSamplingSd3 = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ModelSamplingSD3
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.ModelSamplingSD3)),
                    Model = e.Builder.Connections.Base.Model,
                    Shift = Shift,
                }
            );

            e.Builder.Connections.Base.Model = modelSamplingSd3.Output;
        }

        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected"),
            }
        );
        e.Builder.Connections.Base.VAE = vaeLoader.Output;

        if (SelectedClipType == "flux")
        {
            // DualCLIPLoader
            var clipLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.DualCLIPLoader
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.DualCLIPLoader)),
                    ClipName1 =
                        SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                    ClipName2 =
                        SelectedClip2?.RelativePath ?? throw new ValidationException("No Clip2 Selected"),
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected"),
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
        else
        {
            SetupClipLoaders(e);
        }
    }

    private void SetupDefaultModelLoader(ModuleApplyStepEventArgs e)
    {
        // Load base checkpoint
        var loaderNode =
            SelectedModelLoader is ModelLoader.Default
                ? GetDefaultModelLoader(
                    e,
                    "CheckpointLoader_Base",
                    SelectedModel ?? throw new ValidationException("Model not selected")
                )
                : new ComfyNodeBuilder.CheckpointLoaderNF4
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CheckpointLoaderNF4)),
                    CkptName =
                        SelectedModel?.RelativePath ?? throw new ValidationException("Model not selected"),
                };

        var baseLoader = e.Nodes.AddTypedNode(loaderNode);

        e.Builder.Connections.Base.Model = baseLoader.Output1;
        e.Builder.Connections.Base.VAE = baseLoader.Output3;

        // Use separate clip loaders only when an encoder is actually configured. The flag can
        // be a stale leftover (e.g. an old saved project from a UNet workflow) and checkpoints
        // always bundle their own text encoder, so falling back to it beats failing validation
        // for encoder slots the checkpoint UI doesn't even show.
        if (IsClipModelSelectionEnabled && TextEncoders.Any(slot => slot.SelectedModel is { IsNone: false }))
        {
            SetupClipLoaders(e);
        }
        else
        {
            e.Builder.Connections.Base.Clip = baseLoader.Output2;
        }

        // Load refiner if enabled
        if (IsRefinerSelectionEnabled && SelectedRefiner is { IsNone: false })
        {
            var refinerLoader = e.Nodes.AddTypedNode(
                GetDefaultModelLoader(
                    e,
                    "CheckpointLoader_Refiner",
                    SelectedRefiner ?? throw new ValidationException("Refiner Model enabled but not selected")
                )
            );

            e.Builder.Connections.Refiner.Model = refinerLoader.Output1;
            e.Builder.Connections.Refiner.Clip = refinerLoader.Output2;
            e.Builder.Connections.Refiner.VAE = refinerLoader.Output3;
        }

        // Load VAE override if enabled
        if (IsVaeSelectionEnabled && SelectedVae is { IsNone: false, IsDefault: false })
        {
            var vaeLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.VAELoader
                {
                    Name = "VAELoader",
                    VaeName =
                        SelectedVae?.RelativePath
                        ?? throw new ValidationException("VAE enabled but not selected"),
                }
            );

            e.Builder.Connections.PrimaryVAE = vaeLoader.Output;
        }
    }

    private void SetupClipLoaders(ModuleApplyStepEventArgs e)
    {
        if (
            SelectedClip4 is { IsNone: false }
            && SelectedClip3 is { IsNone: false }
            && SelectedClip2 is { IsNone: false }
            && SelectedClip1 is { IsNone: false }
        )
        {
            var clipLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.QuadrupleCLIPLoader
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.QuadrupleCLIPLoader)),
                    ClipName1 =
                        SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                    ClipName2 =
                        SelectedClip2?.RelativePath ?? throw new ValidationException("No Clip2 Selected"),
                    ClipName3 =
                        SelectedClip3?.RelativePath ?? throw new ValidationException("No Clip3 Selected"),
                    ClipName4 =
                        SelectedClip4?.RelativePath ?? throw new ValidationException("No Clip4 Selected"),
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
        else if (
            SelectedClip3 is { IsNone: false }
            && SelectedClip2 is { IsNone: false }
            && SelectedClip1 is { IsNone: false }
        )
        {
            var clipLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.TripleCLIPLoader
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.TripleCLIPLoader)),
                    ClipName1 =
                        SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                    ClipName2 =
                        SelectedClip2?.RelativePath ?? throw new ValidationException("No Clip2 Selected"),
                    ClipName3 =
                        SelectedClip3?.RelativePath ?? throw new ValidationException("No Clip3 Selected"),
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
        else if (SelectedClip2 is { IsNone: false } && SelectedClip1 is { IsNone: false })
        {
            var clipLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.DualCLIPLoader
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.DualCLIPLoader)),
                    ClipName1 =
                        SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                    ClipName2 =
                        SelectedClip2?.RelativePath ?? throw new ValidationException("No Clip2 Selected"),
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected"),
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
        else if (SelectedClip1 is { IsNone: false })
        {
            var clipLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.CLIPLoader()
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                    ClipName =
                        SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected"),
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
        else
        {
            // No valid encoders configured
            throw new ValidationException(
                "No text encoders configured. Please select at least one encoder model."
            );
        }
    }

    internal class ModelCardModel
    {
        public string? SelectedModelName { get; init; }
        public string? SelectedRefinerName { get; init; }
        public string? SelectedVaeName { get; init; }

        // Legacy encoder fields (for backward compatibility)
        public string? SelectedClip1Name { get; init; }
        public string? SelectedClip2Name { get; init; }
        public string? SelectedClip3Name { get; init; }
        public string? SelectedClip4Name { get; init; }

        // New dynamic encoder list (supports any number of encoders)
        public List<string?>? TextEncoderNames { get; init; }

        public string? SelectedClipType { get; init; }
        public InferenceWorkflowProfile SelectedWorkflowProfile { get; init; } =
            InferenceWorkflowProfile.Auto;
        public string? SelectedDType { get; init; }
        public double Shift { get; init; } = 3.0;
        public ModelLoader ModelLoader { get; init; }
        public int ClipSkip { get; init; } = 1;

        public bool IsVaeSelectionEnabled { get; init; }
        public bool IsRefinerSelectionEnabled { get; init; }
        public bool IsClipSkipEnabled { get; init; }
        public bool IsExtraNetworksEnabled { get; init; }
        public bool IsModelLoaderSelectionEnabled { get; init; }
        public bool IsClipModelSelectionEnabled { get; init; }
        public bool ShowRefinerOption { get; init; }

        public bool IsAdvancedOptionsExpanded { get; init; }
        public bool IsTextEncodersExpanded { get; init; } = true;

        public JsonObject? ExtraNetworks { get; init; }
    }
}
