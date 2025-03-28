using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Avalonia.ViewModels.Inference.Modules;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
[ManagedService]
[RegisterTransient<ModelCardViewModel>]
public partial class ModelCardViewModel(
    IInferenceClientManager clientManager,
    ServiceManager<ViewModelBase> vmFactory,
    TabContext tabContext
) : LoadableViewModelBase, IParametersLoadableState, IComfyStep
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private HybridModelFile? selectedUnetModel;

    [ObservableProperty]
    private bool isRefinerSelectionEnabled;

    [ObservableProperty]
    private bool showRefinerOption = true;

    [ObservableProperty]
    private HybridModelFile? selectedRefiner = HybridModelFile.None;

    [ObservableProperty]
    private HybridModelFile? selectedVae = HybridModelFile.Default;

    [ObservableProperty]
    private bool isVaeSelectionEnabled;

    [ObservableProperty]
    private bool disableSettings;

    [ObservableProperty]
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
    [NotifyPropertyChangedFor(nameof(IsStandaloneModelLoader), nameof(ShowPrecisionSelection))]
    private ModelLoader selectedModelLoader;

    [ObservableProperty]
    private HybridModelFile? selectedClip1;

    [ObservableProperty]
    private HybridModelFile? selectedClip2;

    [ObservableProperty]
    private HybridModelFile? selectedClip3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSd3Clip))]
    private string? selectedClipType;

    [ObservableProperty]
    private string? selectedDType;

    [ObservableProperty]
    private bool enableModelLoaderSelection = true;

    [ObservableProperty]
    private bool isClipModelSelectionEnabled;

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e5m2"];
    public List<string> ClipTypes { get; set; } = ["flux", "sd3"];

    public StackEditableCardViewModel ExtraNetworksStackCardViewModel { get; } =
        new(vmFactory) { Title = Resources.Label_ExtraNetworks, AvailableModules = [typeof(LoraModule)] };

    public IInferenceClientManager ClientManager { get; } = clientManager;

    public List<ModelLoader> ModelLoaders { get; } = Enum.GetValues<ModelLoader>().ToList();

    public bool IsStandaloneModelLoader => SelectedModelLoader is ModelLoader.Unet or ModelLoader.Gguf;
    public bool ShowPrecisionSelection => SelectedModelLoader is ModelLoader.Unet;
    public bool IsSd3Clip => SelectedClipType == "sd3";

    protected override void OnInitialLoaded()
    {
        base.OnInitialLoaded();
        ExtraNetworksStackCardViewModel.CardAdded += ExtraNetworksStackCardViewModelOnCardAdded;
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
                CkptName = model.RelativePath
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
                        StopAtClipLayer = -ClipSkip
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
                SelectedClip1Name = SelectedClip1?.RelativePath,
                SelectedClip2Name = SelectedClip2?.RelativePath,
                SelectedClip3Name = SelectedClip3?.RelativePath,
                SelectedClipType = SelectedClipType,
                IsClipModelSelectionEnabled = IsClipModelSelectionEnabled,
                ModelLoader = SelectedModelLoader,
                ShowRefinerOption = ShowRefinerOption,
                ExtraNetworks = ExtraNetworksStackCardViewModel.SaveStateToJsonObject()
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ModelCardModel>(state);

        SelectedModelLoader = model.ModelLoader;

        if (model.ModelLoader is ModelLoader.Unet or ModelLoader.Gguf)
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

        SelectedClip1 = model.SelectedClip1Name is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x => x.RelativePath == model.SelectedClip1Name);

        SelectedClip2 = model.SelectedClip2Name is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x => x.RelativePath == model.SelectedClip2Name);

        SelectedClip3 = model.SelectedClip3Name is null
            ? HybridModelFile.None
            : ClientManager.ClipModels.FirstOrDefault(x => x.RelativePath == model.SelectedClip3Name);

        SelectedClipType = model.SelectedClipType;

        ClipSkip = model.ClipSkip;

        IsVaeSelectionEnabled = model.IsVaeSelectionEnabled;
        IsRefinerSelectionEnabled = model.IsRefinerSelectionEnabled;
        ShowRefinerOption = model.ShowRefinerOption;
        IsClipSkipEnabled = model.IsClipSkipEnabled;
        IsExtraNetworksEnabled = model.IsExtraNetworksEnabled;
        IsModelLoaderSelectionEnabled = model.IsModelLoaderSelectionEnabled;
        IsClipModelSelectionEnabled = model.IsClipModelSelectionEnabled;

        if (model.ExtraNetworks is not null)
        {
            ExtraNetworksStackCardViewModel.LoadStateFromJsonObject(model.ExtraNetworks);
        }
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.Models.Concat(ClientManager.UnetModels).ToList();

        HybridModelFile? model;

        // First try hash match
        if (parameters.ModelHash is not null)
        {
            model = currentModels.FirstOrDefault(
                m =>
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

        if (model.Local?.SharedFolderType is SharedFolderType.Unet)
        {
            SelectedUnetModel = model;
        }
        else
        {
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
                ModelHash = SelectedUnetModel?.Local?.ConnectedModelInfo?.Hashes.SHA256
            };
        }

        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256
        };
    }

    partial void OnSelectedModelLoaderChanged(ModelLoader value)
    {
        if (value is ModelLoader.Unet or ModelLoader.Gguf)
        {
            if (!IsVaeSelectionEnabled)
                IsVaeSelectionEnabled = true;

            if (!IsClipModelSelectionEnabled)
                IsClipModelSelectionEnabled = true;
        }
    }

    partial void OnSelectedModelChanged(HybridModelFile? value)
    {
        // Update TabContext with the selected model
        tabContext.SelectedModel = value;
        tabContext.NotifyStateChanged(nameof(TabContext.SelectedModel), value);

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

    private void SetupStandaloneModelLoader(ModuleApplyStepEventArgs e)
    {
        if (SelectedModelLoader is ModelLoader.Gguf)
        {
            var checkpointLoader = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.UnetLoaderGGUF
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                    UnetName =
                        SelectedUnetModel?.RelativePath ?? throw new ValidationException("Model not selected")
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
                    WeightDtype = SelectedDType ?? "default"
                }
            );
            e.Builder.Connections.Base.Model = checkpointLoader.Output;
        }

        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected")
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
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected")
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
                        SelectedModel?.RelativePath ?? throw new ValidationException("Model not selected")
                };

        var baseLoader = e.Nodes.AddTypedNode(loaderNode);

        e.Builder.Connections.Base.Model = baseLoader.Output1;
        e.Builder.Connections.Base.VAE = baseLoader.Output3;

        if (IsClipModelSelectionEnabled)
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
                        ?? throw new ValidationException("VAE enabled but not selected")
                }
            );

            e.Builder.Connections.PrimaryVAE = vaeLoader.Output;
        }
    }

    private void SetupClipLoaders(ModuleApplyStepEventArgs e)
    {
        if (
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
                        SelectedClip3?.RelativePath ?? throw new ValidationException("No Clip3 Selected")
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
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected")
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
                    Type = SelectedClipType ?? throw new ValidationException("No Clip Type Selected")
                }
            );
            e.Builder.Connections.Base.Clip = clipLoader.Output;
        }
    }

    internal class ModelCardModel
    {
        public string? SelectedModelName { get; init; }
        public string? SelectedRefinerName { get; init; }
        public string? SelectedVaeName { get; init; }
        public string? SelectedClip1Name { get; init; }
        public string? SelectedClip2Name { get; init; }
        public string? SelectedClip3Name { get; init; }
        public string? SelectedClipType { get; init; }
        public ModelLoader ModelLoader { get; init; }
        public int ClipSkip { get; init; } = 1;

        public bool IsVaeSelectionEnabled { get; init; }
        public bool IsRefinerSelectionEnabled { get; init; }
        public bool IsClipSkipEnabled { get; init; }
        public bool IsExtraNetworksEnabled { get; init; }
        public bool IsModelLoaderSelectionEnabled { get; init; }
        public bool IsClipModelSelectionEnabled { get; init; }
        public bool ShowRefinerOption { get; init; }

        public JsonObject? ExtraNetworks { get; init; }
    }
}
