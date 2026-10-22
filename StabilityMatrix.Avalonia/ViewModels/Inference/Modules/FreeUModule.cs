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
    public FreeUModule(IServiceManager<ViewModelBase> vmFactory)
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

        e.PatchModels(
            (name, model) =>
                e
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.FreeU
                        {
                            Name = e.Nodes.GetUniqueName($"FreeU_{name}"),
                            Model = model,
                            B1 = card.B1,
                            B2 = card.B2,
                            S1 = card.S1,
                            S2 = card.S2,
                        }
                    )
                    .Output
        );
    }
}
