using System.Linq;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<FreeUModule>]
public class FreeUModule : ModuleBase
{
    /// <inheritdoc />
    public FreeUModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "FreeU";
        AddCards(vmFactory.Get<FreeUCardViewModel>());
    }

    /// <summary>
    /// Applies FreeU to the Model property
    /// </summary>
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<FreeUCardViewModel>();

        // Currently applies to all models
        // TODO: Add option to apply to either base or refiner

        foreach (var modelConnections in e.Builder.Connections.Models.Values.Where(m => m.Model is not null))
        {
            var freeUOutput = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.FreeU
                {
                    Name = e.Nodes.GetUniqueName($"FreeU_{modelConnections.Name}"),
                    Model = modelConnections.Model!,
                    B1 = card.B1,
                    B2 = card.B2,
                    S1 = card.S1,
                    S2 = card.S2
                }
            ).Output;

            modelConnections.Model = freeUOutput;
            e.Temp.Base.Model = freeUOutput;
        }
    }
}
