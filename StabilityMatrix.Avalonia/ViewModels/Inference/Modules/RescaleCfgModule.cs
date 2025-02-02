using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<RescaleCfgModule>]
public class RescaleCfgModule : ModuleBase
{
    public RescaleCfgModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "CFG Rescale";
        AddCards(vmFactory.Get<RescaleCfgCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var vm = GetCard<RescaleCfgCardViewModel>();

        foreach (var modelConnections in e.Builder.Connections.Models.Values)
        {
            if (modelConnections.Model is not { } model)
                continue;

            var rescaleCfg = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.RescaleCFG
                {
                    Name = e.Nodes.GetUniqueName("RescaleCFG"),
                    Model = model,
                    Multiplier = vm.Multiplier
                }
            );

            modelConnections.Model = rescaleCfg.Output;

            switch (modelConnections.Name)
            {
                case "Base":
                    e.Temp.Base.Model = rescaleCfg.Output;
                    break;
                case "Refiner":
                    e.Temp.Refiner.Model = rescaleCfg.Output;
                    break;
            }
        }
    }
}
