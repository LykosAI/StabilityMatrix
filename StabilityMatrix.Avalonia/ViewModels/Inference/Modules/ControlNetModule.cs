using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using StabilityMatrix.Avalonia.Controls;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[Transient]
public class ControlNetModule : ModuleBase
{
    /// <inheritdoc />
    public ControlNetModule(ServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "ControlNet";
        AddCards(vmFactory.Get<ControlNetCardViewModel>());
    }

    public IEnumerable<ImageSource> GetInputImages()
    {
        if (GetCard<ControlNetCardViewModel>().SelectImageCardViewModel.ImageSource is { } image)
        {
            yield return image;
        }
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<ControlNetCardViewModel>();

        var imageLoad = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = e.Nodes.GetUniqueName("ControlNet_LoadImage"),
                Image =
                    card.SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached(
                        "Inference"
                    ) ?? throw new ValidationException()
            }
        );

        var controlNetLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetLoader
            {
                Name = e.Nodes.GetUniqueName("ControlNetLoader"),
                ControlNetName = card.SelectedModel?.FileName ?? throw new ValidationException(),
            }
        );

        var controlNetApply = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetApplyAdvanced
            {
                Name = e.Nodes.GetUniqueName("ControlNet"),
                Image = imageLoad.Output1,
                ControlNet = controlNetLoader.Output,
                Positive = e.Temp.Conditioning.Positive,
                Negative = e.Temp.Conditioning.Negative,
                Strength = card.Strength,
                StartPercent = card.StartPercent,
                EndPercent = card.EndPercent,
            }
        );

        e.Temp.Conditioning = (controlNetApply.Output1, controlNetApply.Output2);
    }
}
