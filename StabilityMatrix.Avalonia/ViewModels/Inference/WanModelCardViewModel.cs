using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// Optional low-noise expert model for the Wan 2.2 14B MoE. When <see cref="IsLowNoiseModelEnabled"/>
    /// is set and this is selected, <see cref="SelectedModel"/> acts as the high-noise expert and the
    /// sampler runs a two-pass high-noise -> low-noise generation.
    /// </summary>
    [ObservableProperty]
    private HybridModelFile? selectedLowNoiseModel;

    [ObservableProperty]
    private bool isLowNoiseModelEnabled;

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

    [ObservableProperty]
    private string? selectedDType = "fp8_e4m3fn_fast";

    [ObservableProperty]
    private bool isClipVisionEnabled;

    [ObservableProperty]
    private double shift = 8.0d;

    public IInferenceClientManager ClientManager { get; } = clientManager;

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
        // WanModelCard only uses UNet models
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

        if (IsClipVisionEnabled && SelectedClipVisionModel == null)
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
        // Loads a UNet (GGUF or safetensors) and applies the Wan ModelSamplingSD3 shift.
        // Used for both the high-noise and (optional) low-noise expert of the Wan 2.2 14B MoE.
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
        if (IsLowNoiseModelEnabled && SelectedLowNoiseModel is { } lowNoiseModel)
        {
            lowNoiseConnections = new ModelConnections(LowNoiseModelKey)
            {
                Model = LoadUnetModel(lowNoiseModel),
            };
            e.Builder.Connections.WanMoeBoundary = MoeBoundary;
        }

        var clipLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPLoader
            {
                Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.CLIPLoader)),
                ClipName =
                    SelectedClipModel?.RelativePath
                    ?? throw new ValidationException("No Clip Model Selected"),
                Type = "wan",
            }
        );

        e.Builder.Connections.Base.Clip = clipLoader.Output;

        // Share the text encoder with the low-noise expert so its model-side LoRA patches apply.
        if (lowNoiseConnections is not null)
        {
            lowNoiseConnections.Clip = clipLoader.Output;
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

        if (!IsClipVisionEnabled)
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
