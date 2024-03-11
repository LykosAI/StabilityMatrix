using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
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
                    // Use width if valid, else default of 512
                    Resolution = card.Width is <= 2048 and > 0 ? card.Width : 512
                }
            );

            image = aioPreprocessor.Output;
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
                Positive = e.Temp.Conditioning?.Positive ?? throw new ArgumentException("No Conditioning"),
                Negative = e.Temp.Conditioning?.Negative ?? throw new ArgumentException("No Conditioning"),
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
                    Image = image,
                    ControlNet = controlNetLoader.Output,
                    Positive = e.Temp.RefinerConditioning.Positive,
                    Negative = e.Temp.RefinerConditioning.Negative,
                    Strength = card.Strength,
                    StartPercent = card.StartPercent,
                    EndPercent = card.EndPercent,
                }
            );

            e.Temp.RefinerConditioning = (controlNetRefinerApply.Output1, controlNetRefinerApply.Output2);
        }
    }
}
