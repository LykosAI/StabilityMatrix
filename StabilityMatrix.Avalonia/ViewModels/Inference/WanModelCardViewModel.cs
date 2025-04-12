using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
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

namespace StabilityMatrix.Avalonia.ViewModels.Inference;

[View(typeof(WanModelCard))]
[ManagedService]
[RegisterTransient<WanModelCardViewModel>]
public partial class WanModelCardViewModel(
    IInferenceClientManager clientManager,
    IServiceManager<ViewModelBase> vmFactory
) : LoadableViewModelBase, IParametersLoadableState, IComfyStep
{
    [ObservableProperty]
    private HybridModelFile? selectedModel;

    [ObservableProperty]
    private HybridModelFile? selectedClipModel;

    [ObservableProperty]
    private HybridModelFile? selectedClipVisionModel;

    [ObservableProperty]
    private HybridModelFile? selectedVae;

    [ObservableProperty]
    private string? selectedDType = "fp8_e4m3fn_fast";

    [ObservableProperty]
    private bool isClipVisionEnabled;

    [ObservableProperty]
    private double shift = 8.0d;

    public IInferenceClientManager ClientManager { get; } = clientManager;

    public StackEditableCardViewModel ExtraNetworksStackCardViewModel { get; } =
        new(vmFactory) { Title = Resources.Label_ExtraNetworks, AvailableModules = [typeof(LoraModule)] };

    public List<string> WeightDTypes { get; set; } = ["default", "fp8_e4m3fn", "fp8_e4m3fn_fast", "fp8_e5m2"];

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

        if (IsClipVisionEnabled && SelectedClipVisionModel == null)
        {
            var dialog = DialogHelper.CreateMarkdownDialog(
                "Please select a CLIP Vision model to continue.",
                "No CLIP Vision Model Selected"
            );
            await dialog.ShowAsync();
            return false;
        }

        return true;
    }

    public void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var modelLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.UNETLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.UNETLoader)),
                UnetName = SelectedModel?.RelativePath ?? throw new ValidationException("Model not selected"),
                WeightDtype = SelectedDType ?? "fp8_e4m3fn_fast"
            }
        );

        var modelSamplingSd3 = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ModelSamplingSD3
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.ModelSamplingSD3)),
                Model = modelLoader.Output,
                Shift = Shift
            }
        );

        e.Builder.Connections.Base.Model = modelSamplingSd3.Output;

        var clipLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                ClipName =
                    SelectedClipModel?.RelativePath
                    ?? throw new ValidationException("No Clip Model Selected"),
                Type = "wan"
            }
        );

        e.Builder.Connections.Base.Clip = clipLoader.Output;

        var vaeLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.VAELoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.VAELoader)),
                VaeName = SelectedVae?.RelativePath ?? throw new ValidationException("No VAE Selected")
            }
        );
        e.Builder.Connections.Base.VAE = vaeLoader.Output;
        e.Builder.Connections.PrimaryVAE = vaeLoader.Output;

        if (ExtraNetworksStackCardViewModel.Cards.OfType<LoraModule>().Any(x => x.IsEnabled))
        {
            ExtraNetworksStackCardViewModel.ApplyStep(e);
        }

        if (!IsClipVisionEnabled)
            return;

        var clipVisionLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPVisionLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPVisionLoader)),
                ClipName =
                    SelectedClipVisionModel?.RelativePath
                    ?? throw new ValidationException("No Clip Vision Model Selected")
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

        SelectedModel = model;
    }

    public GenerationParameters SaveStateToParameters(GenerationParameters parameters)
    {
        return parameters with
        {
            ModelName = SelectedModel?.FileName,
            ModelHash = SelectedModel?.Local?.ConnectedModelInfo?.Hashes.SHA256
        };
    }
}
