using System;
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

    protected override IEnumerable<ImageSource> GetInputImages()
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
                    ) ?? throw new ValidationException("No ImageSource")
            }
        );

        var controlNetLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetLoader
            {
                Name = e.Nodes.GetUniqueName("ControlNetLoader"),
                ControlNetName =
                    card.SelectedModel?.FileName
                    ?? throw new ValidationException("No SelectedModel"),
            }
        );

        var controlNetApply = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetApplyAdvanced
            {
                Name = e.Nodes.GetUniqueName("ControlNetApply"),
                Image = imageLoad.Output1,
                ControlNet = controlNetLoader.Output,
                Positive =
                    e.Temp.Conditioning?.Positive ?? throw new ArgumentException("No Conditioning"),
                Negative =
                    e.Temp.Conditioning?.Negative ?? throw new ArgumentException("No Conditioning"),
                Strength = card.Strength,
                StartPercent = card.StartPercent,
                EndPercent = card.EndPercent,
            }
        );

        e.Temp.Conditioning = (controlNetApply.Output1, controlNetApply.Output2);

        // Refiner if available
        if (e.Temp.RefinerConditioning is not null)
        {
            var controlNetRefinerApply = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ControlNetApplyAdvanced
                {
                    Name = e.Nodes.GetUniqueName("Refiner_ControlNetApply"),
                    Image = imageLoad.Output1,
                    ControlNet = controlNetLoader.Output,
                    Positive = e.Temp.RefinerConditioning.Value.Positive,
                    Negative = e.Temp.RefinerConditioning.Value.Negative,
                    Strength = card.Strength,
                    StartPercent = card.StartPercent,
                    EndPercent = card.EndPercent,
                }
            );

            e.Temp.RefinerConditioning = (
                controlNetRefinerApply.Output1,
                controlNetRefinerApply.Output2
            );
        }
    }
}
