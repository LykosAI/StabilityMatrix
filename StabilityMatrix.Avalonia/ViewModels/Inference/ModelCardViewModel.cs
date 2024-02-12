using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(ModelCard))]
[ManagedService]
[Transient]
public partial class ModelCardViewModel(IInferenceClientManager clientManager)
    : LoadableViewModelBase,
        IParametersLoadableState,
        IComfyStep
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private bool isRefinerSelectionEnabled;

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

    public IInferenceClientManager ClientManager { get; } = clientManager;

    public async Task<bool> ValidateModel()
    {
        if (SelectedModel != null)
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
    > GetModelLoader(ModuleApplyStepEventArgs e, string nodeName, HybridModelFile model)
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
        // Load base checkpoint
        var baseLoader = e.Nodes.AddTypedNode(
            GetModelLoader(
                e,
                "CheckpointLoader_Base",
                SelectedModel ?? throw new ValidationException("Model not selected")
            )
        );

        e.Builder.Connections.Base.Model = baseLoader.Output1;
        e.Builder.Connections.Base.Clip = baseLoader.Output2;
        e.Builder.Connections.Base.VAE = baseLoader.Output3;

        // Load refiner if enabled
        if (IsRefinerSelectionEnabled && SelectedRefiner is { IsNone: false })
        {
            var refinerLoader = e.Nodes.AddTypedNode(
                GetModelLoader(
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
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new ModelCardModel
            {
                SelectedModelName = SelectedModel?.RelativePath,
                SelectedVaeName = SelectedVae?.RelativePath,
                SelectedRefinerName = SelectedRefiner?.RelativePath,
                ClipSkip = ClipSkip,
                IsVaeSelectionEnabled = IsVaeSelectionEnabled,
                IsRefinerSelectionEnabled = IsRefinerSelectionEnabled,
                IsClipSkipEnabled = IsClipSkipEnabled
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<ModelCardModel>(state);

        SelectedModel = model.SelectedModelName is null
            ? null
            : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);

        SelectedVae = model.SelectedVaeName is null
            ? HybridModelFile.Default
            : ClientManager.VaeModels.FirstOrDefault(x => x.RelativePath == model.SelectedVaeName);

        SelectedRefiner = model.SelectedRefinerName is null
            ? HybridModelFile.None
            : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedRefinerName);

        ClipSkip = model.ClipSkip;

        IsVaeSelectionEnabled = model.IsVaeSelectionEnabled;
        IsRefinerSelectionEnabled = model.IsRefinerSelectionEnabled;
        IsClipSkipEnabled = model.IsClipSkipEnabled;
    }

    /// <inheritdoc />
    public void LoadStateFromParameters(GenerationParameters parameters)
    {
        if (parameters.ModelName is not { } paramsModelName)
            return;

        var currentModels = ClientManager.Models;

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

        if (model is not null)
        {
            SelectedModel = model;
        }
    }

    /// <inheritdoc />
    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256
        };
    }

    internal class ModelCardModel
    {
        public string? SelectedModelName { get; init; }
        public string? SelectedRefinerName { get; init; }
        public string? SelectedVaeName { get; init; }
        public int ClipSkip { get; init; } = 1;

        public bool IsVaeSelectionEnabled { get; init; }
        public bool IsRefinerSelectionEnabled { get; init; }
        public bool IsClipSkipEnabled { get; init; }
    }
}
