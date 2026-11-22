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
    /// Applies NRS to the Model property
    /// </summary>
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<NrsCardViewModel>();

        // Currently applies to all models
        // TODO: Add option to apply to either base or refiner

        e.PatchModels(
            (name, model) =>
                e
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.NRS
                        {
                            Name = e.Nodes.GetUniqueName($"NRS_{name}"),
                            Model = model,
                            Skew = card.Skew,
                            Stretch = card.Stretch,
                            Squash = card.Squash,
                        }
                    )
                    .Output
        );
    }
}
