using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<NRSModule>]
public class NRSModule : ModuleBase
{
    /// <inheritdoc />
    public NRSModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Negative Rejection Steering (NRS)";
        AddCards(vmFactory.Get<NrsCardViewModel>());
    }

    /// <summary>
    /// Applies FreeU to the Model property
    /// </summary>
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<NrsCardViewModel>();

        // Currently applies to all models
        // TODO: Add option to apply to either base or refiner

        foreach (var modelConnections in e.Builder.Connections.Models.Values.Where(m => m.Model is not null))
        {
            var nrsOutput = e
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.NRS
                    {
                        Name = e.Nodes.GetUniqueName($"NRS_{modelConnections.Name}"),
                        Model = modelConnections.Model!,
                        Skew = card.Skew,
                        Stretch = card.Stretch,
                        Squash = card.Squash,
                    }
                )
                .Output;

            modelConnections.Model = nrsOutput;
            e.Temp.Base.Model = nrsOutput;
        }
    }
}
