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
    public DiscreteModelSamplingModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Discrete Model Sampling";
        AddCards(vmFactory.Get<DiscreteModelSamplingCardViewModel>());
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var vm = GetCard<DiscreteModelSamplingCardViewModel>();

        foreach (var modelConnections in e.Builder.Connections.Models.Values)
        {
            if (modelConnections.Model is not { } model)
                continue;

            var modelSamplingDiscrete = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ModelSamplingDiscrete
                {
                    Name = e.Nodes.GetUniqueName("ModelSamplingDiscrete"),
                    Model = model,
                    Sampling = vm.SelectedSamplingMethod,
                    Zsnr = vm.IsZsnrEnabled
                }
            );

            modelConnections.Model = modelSamplingDiscrete.Output;
            e.Temp.Base.Model = modelSamplingDiscrete.Output;
        }
    }
}
