using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<FluxGuidanceModule>]
public class FluxGuidanceModule : ModuleBase
{
    public FluxGuidanceModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Use Flux Guidance";
    }

    protected override void OnApplyStep(ModuleApplyStepEventArgs e) { }
}
