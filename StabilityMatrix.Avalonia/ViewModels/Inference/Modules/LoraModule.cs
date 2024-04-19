using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
public class LoraModule : ModuleBase
{
    public LoraModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Lora";
        AddCards(vmFactory.Get<ExtraNetworkCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<ExtraNetworkCardViewModel>();

        // Skip if no lora model
        if (card.SelectedModel is not { } selectedLoraModel)
            return;

        // Add lora conditioning to all models
        foreach (var modelConnections in e.Builder.Connections.Models.Values)
        {
            if (modelConnections.Model is not { } model || modelConnections.Clip is not { } clip)
                continue;

            var loraLoader = e.Nodes.AddNamedNode(
                ComfyNodeBuilder.LoraLoader(
                    e.Nodes.GetUniqueName($"Loras_{modelConnections.Name}"),
                    model,
                    clip,
                    selectedLoraModel.RelativePath,
                    card.ModelWeight,
                    card.ClipWeight
                )
            );

            // Replace current model and clip with lora loaded model and clip
            modelConnections.Model = loraLoader.Output1;
            modelConnections.Clip = loraLoader.Output2;
        }
    }
}
