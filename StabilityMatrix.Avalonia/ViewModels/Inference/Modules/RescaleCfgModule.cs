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
    public RescaleCfgModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "CFG Rescale";
        AddCards(vmFactory.Get<RescaleCfgCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var vm = GetCard<RescaleCfgCardViewModel>();

        e.PatchModels(
            (_, model) =>
                e
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.RescaleCFG
                        {
                            Name = e.Nodes.GetUniqueName("RescaleCFG"),
                            Model = model,
                            Multiplier = vm.Multiplier,
                        }
                    )
                    .Output
        );
    }
}
