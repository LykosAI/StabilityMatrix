using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(UnetModelCard))]
[ManagedService]
[RegisterTransient<UnetModelCardViewModel>]
public partial class UnetModelCardViewModel(IInferenceClientManager clientManager)
    : LoadableViewModelBase,
        IParametersLoadableState,
        IComfyStep
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private HybridModelFile? selectedVae;

    [ObservableProperty]
    private HybridModelFile? selectedClip1;

    [ObservableProperty]
    private HybridModelFile? selectedClip2;

    [ObservableProperty]
    private string selectedDType = "default";

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e5m2"];

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

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var checkpointLoader = e.Nodes.AddTypedNode(GetModelLoader(e, SelectedModel!, SelectedDType));
        e.Builder.Connections.Base.Model = checkpointLoader.Output;

        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected")
            }
        );
        e.Builder.Connections.Base.VAE = vaeLoader.Output;

        // DualCLIPLoader
        var clipLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.DualCLIPLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.DualCLIPLoader)),
                ClipName1 = SelectedClip1?.RelativePath ?? throw new ValidationException("No Clip1 Selected"),
                ClipName2 = SelectedClip2?.RelativePath ?? throw new ValidationException("No Clip2 Selected"),
                Type = "flux"
            }
        );
        e.Builder.Connections.Base.Clip = clipLoader.Output;
    }

    private static ComfyTypedNodeBase<ModelNodeConnection> GetModelLoader(
        ModuleApplyStepEventArgs e,
        HybridModelFile model,
        string selectedDType
    )
    {
        // Simple loader for UNET
        return new ComfyNodeBuilder.UNETLoader
        {
            Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
            UnetName = model.RelativePath,
            WeightDtype = selectedDType
        };
    }

    /// <inheritdoc />
    public override JsonObject SaveStateToJsonObject()
    {
        return SerializeModel(
            new UnetModelCardModel
            {
                SelectedModelName = SelectedModel?.RelativePath,
                SelectedVaeName = SelectedVae?.RelativePath,
                SelectedClip1Name = SelectedClip1?.RelativePath,
                SelectedClip2Name = SelectedClip2?.RelativePath
            }
        );
    }

    /// <inheritdoc />
    public override void LoadStateFromJsonObject(JsonObject state)
    {
        var model = DeserializeModel<UnetModelCardModel>(state);

        SelectedModel = model.SelectedModelName is null
            ? null
            : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedModelName);

        SelectedVae = model.SelectedVaeName is null
            ? HybridModelFile.Default
            : ClientManager.VaeModels.FirstOrDefault(x => x.RelativePath == model.SelectedVaeName);

        SelectedClip1 = model.SelectedClip1Name is null
            ? HybridModelFile.None
            : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedClip1Name);

        SelectedClip2 = model.SelectedClip2Name is null
            ? HybridModelFile.None
            : ClientManager.Models.FirstOrDefault(x => x.RelativePath == model.SelectedClip2Name);
    }

    internal class UnetModelCardModel
    {
        public string? SelectedModelName { get; set; }
        public string? SelectedVaeName { get; set; }
        public string? SelectedClip1Name { get; set; }
        public string? SelectedClip2Name { get; set; }
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
}
