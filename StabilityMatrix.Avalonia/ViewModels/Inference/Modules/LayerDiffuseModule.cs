using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterScoped<LayerDiffuseModule>]
public class LayerDiffuseModule : ModuleBase
{
    /// <inheritdoc />
    public LayerDiffuseModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Layer Diffuse";
        AddCards(vmFactory.Get<LayerDiffuseCardViewModel>());
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<LayerDiffuseCardViewModel>();
        card.ApplyStep(e);
    }
}
