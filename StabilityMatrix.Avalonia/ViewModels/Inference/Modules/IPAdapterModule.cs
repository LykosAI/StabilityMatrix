using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterTransient<IPAdapterModule>]
public class IPAdapterModule : ModuleBase
{
    /// <inheritdoc />
    public IPAdapterModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "IPAdapter";
        AddCards(vmFactory.Get<IPAdapterCardViewModel>());
    }

    protected override IEnumerable<ImageSource> GetInputImages()
    {
        if (
            IsEnabled
            && GetCard<IPAdapterCardViewModel>().SelectImageCardViewModel
                is { ImageSource: { } image, IsImageFileNotFound: false }
        )
        {
            yield return image;
        }
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<IPAdapterCardViewModel>();

        var image = e
            .Nodes.AddTypedNode(
                new ComfyNodeBuilder.LoadImage
                {
                    Name = e.Nodes.GetUniqueName("IPAdapter_LoadImage"),
                    Image =
                        card.SelectImageCardViewModel.ImageSource?.GetHashGuidFileNameCached("Inference")
                        ?? throw new ValidationException("No ImageSource"),
                }
            )
            .Output1;

        var ipAdapterLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.IPAdapterModelLoader
            {
                Name = e.Nodes.GetUniqueName("IPAdapterModelLoader"),
                IpadapterFile =
                    card.SelectedModel?.RelativePath ?? throw new ValidationException("No SelectedModel"),
            }
        );

        var clipVisionLoader = e.Nodes.AddTypedNode(
            new ComfyNodeBuilder.CLIPVisionLoader
            {
                Name = e.Nodes.GetUniqueName("IPAdapter_CLIPVisionLoader"),
                ClipName =
                    card.SelectedClipVisionModel?.RelativePath
                    ?? throw new ValidationException("No SelectedClipVisionModel"),
            }
        );

        // Patch each loaded model (Base, Refiner if present) with the IPAdapter
        foreach (var modelConnections in e.Temp.Models.Values)
        {
            if (modelConnections.Model is not { } model)
                continue;

            var ipAdapterApply = e.Nodes.AddTypedNode(
                new ComfyNodeBuilder.IPAdapterAdvanced
                {
                    Name = e.Nodes.GetUniqueName($"IPAdapter_{modelConnections.Name}"),
                    Model = model,
                    Ipadapter = ipAdapterLoader.Output,
                    Image = image,
                    Weight = card.Weight,
                    WeightType = card.SelectedWeightType,
                    StartAt = card.StartAt,
                    EndAt = card.EndAt,
                    ClipVision = clipVisionLoader.Output,
                }
            );

            modelConnections.Model = ipAdapterApply.Output;
        }
    }
}
