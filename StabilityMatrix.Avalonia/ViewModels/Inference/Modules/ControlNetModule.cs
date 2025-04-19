using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Injectio.Attributes;
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
[RegisterTransient<ControlNetModule>]
public class ControlNetModule : ModuleBase
{
    /// <inheritdoc />
    public ControlNetModule(IServiceManager<ViewModelBase> vmFactory)
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

        var image = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = e.Nodes.GetUniqueName("ControlNet_LoadImage"),
                Image =
                    card.SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                    ?? throw new ValidationException("No ImageSource")
            }
        ).Output1;

        if (card.SelectedPreprocessor is { } preprocessor && preprocessor != ComfyAuxPreprocessor.None)
        {
            var aioPreprocessor = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.AIOPreprocessor
                {
                    Name = e.Nodes.GetUniqueName("ControlNet_Preprocessor"),
                    Image = image,
                    Preprocessor = preprocessor.ToString(),
                    // Use lower of width and height for resolution
                    Resolution = Math.Min(card.Width, card.Height)
                }
            );

            image = aioPreprocessor.Output;
        }

        // If ReferenceOnly is selected, use special node
        if (card.SelectedModel == RemoteModels.ControlNetReferenceOnlyModel)
        {
            // We need to rescale image to be the current primary size if it's not already
            var originalPrimary = e.Temp.Primary!.Unwrap();
            var originalPrimarySize = e.Builder.Connections.PrimarySize;

            if (card.SelectImageCardViewModel.CurrentBitmapSize != originalPrimarySize)
            {
                var scaled = e.Builder.Group_Upscale(
                    e.Nodes.GetUniqueName("ControlNet_Rescale"),
                    image,
                    e.Temp.GetDefaultVAE(),
                    ComfyUpscaler.NearestExact,
                    originalPrimarySize.Width,
                    originalPrimarySize.Height
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

            var referenceOnlyModel = controlNetReferenceOnly.Output1;

            // If ControlNet strength is not 1, add Model Merge
            if (Math.Abs(card.Strength - 1) > 0.01)
            {
                var modelBlend = e.Nodes.AddTypedNode(
                    new ComfyNodeBuilder.ModelMergeSimple
                    {
                        Name = e.Nodes.GetUniqueName("ControlNet_ReferenceOnly_ModelMerge"),
                        Model1 = referenceOnlyModel,
                        Model2 = e.Temp.GetRefinerOrBaseModel(),
                        // Where 0 is full reference only, 1 is full original
                        Ratio = 1 - card.Strength
                    }
                );

                referenceOnlyModel = modelBlend.Output;
            }

            // Set output as new primary and model source
            if (model == e.Temp.Refiner.Model)
            {
                e.Temp.Refiner.Model = referenceOnlyModel;
            }
            else
            {
                e.Temp.Base.Model = referenceOnlyModel;
            }
            e.Temp.Primary = controlNetReferenceOnly.Output2;

            // Indicate that the Primary latent has been temp batched
            // https://github.com/comfyanonymous/ComfyUI_experiments/issues/11

            e.Temp.IsPrimaryTempBatched = true;
            // Index 0 is the original image, index 1 is the reference only latent
            e.Temp.PrimaryTempBatchPickIndex = 1;

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
                Image = image,
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
                    Image = image,
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
