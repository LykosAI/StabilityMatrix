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

        e.PatchModels(
            (_, model) =>
                e
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.SkipLayerGuidanceDiTSimple
                        {
                            Name = e.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.SkipLayerGuidanceDiTSimple)),
                            Model = model,
                            DoubleLayers = vm.DoubleLayers,
                            SingleLayers = vm.SingleLayers,
                            StartPercent = vm.StartPercent,
                            EndPercent = vm.EndPercent,
                        }
                    )
                    .Output
        );
    }
}
