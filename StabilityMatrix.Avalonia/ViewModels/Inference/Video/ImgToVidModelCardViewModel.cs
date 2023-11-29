using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Video;

[View(typeof(ModelCard))]
[ManagedService]
[Transient]
public class ImgToVidModelCardViewModel : ModelCardViewModel
{
    public ImgToVidModelCardViewModel(IInferenceClientManager clientManager)
        : base(clientManager)
    {
        DisableSettings = true;
    }

    public override void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var imgToVidLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ImageOnlyCheckpointLoader
            {
                Name = "ImageOnlyCheckpointLoader",
                CkptName =
                    SelectedModel?.RelativePath
                    ?? throw new ValidationException("Model not selected")
            }
        );

        e.Builder.Connections.BaseModel = imgToVidLoader.Output1;
        e.Builder.Connections.BaseClipVision = imgToVidLoader.Output2;
        e.Builder.Connections.BaseVAE = imgToVidLoader.Output3;
    }
}
