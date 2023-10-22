using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
public class SaveImageModule : ModuleBase
{
    /// <inheritdoc />
    public SaveImageModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Save Intermediary Image";
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var preview = e.Builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = e.Builder.Nodes.GetUniqueName("SaveImage"),
                Images = e.Builder.GetPrimaryAsImage()
            }
        );

        e.Builder.Connections.OutputNodes.Add(preview);
    }
}
