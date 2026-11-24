using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<DiscreteModelSamplingModule>]
public class DiscreteModelSamplingModule : ModuleBase
{
    public DiscreteModelSamplingModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Discrete Model Sampling";
        AddCards(vmFactory.Get<DiscreteModelSamplingCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var vm = GetCard<DiscreteModelSamplingCardViewModel>();

        e.PatchModels(
            (_, model) =>
                e
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.ModelSamplingDiscrete
                        {
                            Name = e.Nodes.GetUniqueName("ModelSamplingDiscrete"),
                            Model = model,
                            Sampling = vm.SelectedSamplingMethod,
                            Zsnr = vm.IsZsnrEnabled,
                        }
                    )
                    .Output
        );
    }
}
