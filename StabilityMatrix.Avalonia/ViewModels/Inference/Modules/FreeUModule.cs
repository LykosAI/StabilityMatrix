using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
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

        // Currently applies to both base and refiner model
        // TODO: Add option to apply to either base or refiner

        if (e.Builder.Connections.BaseModel is not null)
        {
            e.Builder.Connections.BaseModel = e.Nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.FreeU
                    {
                        Name = e.Nodes.GetUniqueName("FreeU"),
                        Model = e.Builder.Connections.BaseModel,
                        B1 = card.B1,
                        B2 = card.B2,
                        S1 = card.S1,
                        S2 = card.S2
                    }
                )
                .Output;
        }

        if (e.Builder.Connections.RefinerModel is not null)
        {
            e.Builder.Connections.RefinerModel = e.Nodes
                .AddTypedNode(
                    new ComfyNodeBuilder.FreeU
                    {
                        Name = e.Nodes.GetUniqueName("Refiner_FreeU"),
                        Model = e.Builder.Connections.RefinerModel,
                        B1 = card.B1,
                        B2 = card.B2,
                        S1 = card.S1,
                        S2 = card.S2
                    }
                )
                .Output;
        }
    }
}
