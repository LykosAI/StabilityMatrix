using System.ComponentModel.DataAnnotations;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Video;

[View(typeof(ModelCard))]
[ManagedService]
[RegisterTransient<ImgToVidModelCardViewModel>]
public class ImgToVidModelCardViewModel : ModelCardViewModel
{
    public ImgToVidModelCardViewModel(
        IInferenceClientManager clientManager,
        IServiceManager<ViewModelBase> vmFactory
    )
        : base(clientManager, vmFactory)
    {
        DisableSettings = true;
    }

    public override void ApplyStep(ModuleApplyStepEventArgs e)
    {
        var imgToVidLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ImageOnlyCheckpointLoader
            {
                Name = "ImageOnlyCheckpointLoader",
                CkptName = SelectedModel?.RelativePath ?? throw new ValidationException("Model not selected")
            }
        );

        e.Builder.Connections.Base.Model = imgToVidLoader.Output1;
        e.Builder.Connections.BaseClipVision = imgToVidLoader.Output2;
        e.Builder.Connections.Base.VAE = imgToVidLoader.Output3;
    }
}
