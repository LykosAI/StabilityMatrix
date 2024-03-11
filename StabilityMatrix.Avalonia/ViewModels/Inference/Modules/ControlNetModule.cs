using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models.Api.Comfy;
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
        if (
            IsEnabled
            && GetCard<ControlNetCardViewModel>().SelectImageCardViewModel
                is { ImageSource: { } image, IsImageFileNotFound: false }
        )
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
                    card.SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                    ?? throw new ValidationException("No ImageSource")
            }
        );

        // If ReferenceOnly is selected, use special node
        if (card.SelectedModel == RemoteModels.ControlNetReferenceOnlyModel)
        {
            // We need to rescale image to be the current primary size if it's not already
            var primarySize = e.Builder.Connections.PrimarySize;
            if (card.SelectImageCardViewModel.CurrentBitmapSize != primarySize)
            {
                var scaled = e.Builder.Group_Upscale(
                    e.Nodes.GetUniqueName("ControlNet_Rescale"),
                    image,
                    e.Temp.GetDefaultVAE(),
                    ComfyUpscaler.NearestExact,
                    primarySize.Width,
                    primarySize.Width
                );
                e.Temp.Primary = scaled;
            }
            else
            {
                e.Temp.Primary = image;
            }

            // Set image as new latent source, add reference only node
            var model = e.Temp.GetRefinerOrBaseModel();
            var controlNetReferenceOnly = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ReferenceOnlySimple
                {
                    Name = e.Nodes.GetUniqueName("ControlNet_ReferenceOnly"),
                    Reference = e.Builder.GetPrimaryAsLatent(
                        e.Temp.Primary,
                        e.Builder.Connections.GetDefaultVAE()
                    ),
                    Model = model
                }
            );

            // Set output as new primary and model source
            if (model == e.Temp.Refiner.Model)
            {
                e.Temp.Refiner.Model = controlNetReferenceOnly.Output1;
            }
            else
            {
                e.Temp.Base.Model = controlNetReferenceOnly.Output1;
            }
            e.Temp.Primary = controlNetReferenceOnly.Output2;

            return;
        }

        var controlNetLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetLoader
            {
                Name = e.Nodes.GetUniqueName("ControlNetLoader"),
                ControlNetName =
                    card.SelectedModel?.RelativePath ?? throw new ValidationException("No SelectedModel"),
            }
        );

        var controlNetApply = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.ControlNetApplyAdvanced
            {
                Name = e.Nodes.GetUniqueName("ControlNetApply"),
                Image = imageLoad.Output1,
                ControlNet = controlNetLoader.Output,
                Positive = e.Temp.Base.Conditioning!.Unwrap().Positive,
                Negative = e.Temp.Base.Conditioning.Negative,
                Strength = card.Strength,
                StartPercent = card.StartPercent,
                EndPercent = card.EndPercent,
            }
        );

        e.Temp.Base.Conditioning = (controlNetApply.Output1, controlNetApply.Output2);

        // Refiner if available
        if (e.Temp.Refiner.Conditioning is not null)
        {
            var controlNetRefinerApply = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.ControlNetApplyAdvanced
                {
                    Name = e.Nodes.GetUniqueName("Refiner_ControlNetApply"),
                    Image = imageLoad.Output1,
                    ControlNet = controlNetLoader.Output,
                    Positive = e.Temp.Refiner.Conditioning!.Unwrap().Positive,
                    Negative = e.Temp.Refiner.Conditioning.Negative,
                    Strength = card.Strength,
                    StartPercent = card.StartPercent,
                    EndPercent = card.EndPercent,
                }
            );

            e.Temp.Refiner.Conditioning = (controlNetRefinerApply.Output1, controlNetRefinerApply.Output2);
        }
    }
}
