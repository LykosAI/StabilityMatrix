using System;
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

        e.Temp.Model = e.Nodes
            .AddNamedNode(
                ComfyNodeBuilder.FreeU(
                    e.Nodes.GetUniqueName("FreeU"),
                    e.Temp.Model
                        ?? throw new ArgumentException(
                            "Temp.Model not set on ModuleApplyStepEventArgs"
                        ),
                    card.B1,
                    card.B2,
                    card.S1,
                    card.S2
                )
            )
            .Output;
    }
}
