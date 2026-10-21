using Injectio.Attributes;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

/// <summary>
/// Skip Layer Guidance: skips chosen DiT blocks on the negative pass
/// (SkipLayerGuidanceDiTSimple). Only has an effect when CFG is above 1.
/// </summary>
[ManagedService]
[RegisterTransient<SkipLayerGuidanceModule>]
public class SkipLayerGuidanceModule : ModuleBase
{
    public SkipLayerGuidanceModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = Resources.Label_SkipLayerGuidance;
        AddCards(vmFactory.Get<SkipLayerGuidanceCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var vm = GetCard<SkipLayerGuidanceCardViewModel>();

        foreach (var modelConnections in e.Builder.Connections.Models.Values)
        {
            if (modelConnections.Model is not { } model)
                continue;

            var slg = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.SkipLayerGuidanceDiTSimple
                {
                    Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.SkipLayerGuidanceDiTSimple)),
                    Model = model,
                    DoubleLayers = vm.DoubleLayers,
                    SingleLayers = vm.SingleLayers,
                    StartPercent = vm.StartPercent,
                    EndPercent = vm.EndPercent,
                }
            );

            modelConnections.Model = slg.Output;

            switch (modelConnections.Name)
            {
                case "Base":
                    e.Temp.Base.Model = slg.Output;
                    break;
                case "Refiner":
                    e.Temp.Refiner.Model = slg.Output;
                    break;
            }
        }
    }
}
