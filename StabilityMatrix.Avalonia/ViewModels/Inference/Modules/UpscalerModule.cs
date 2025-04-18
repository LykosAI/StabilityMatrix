using System;
using System.ComponentModel.DataAnnotations;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

[ManagedService]
[RegisterScoped<UpscalerModule>]
public class UpscalerModule : ModuleBase
{
    /// <inheritdoc />
    public UpscalerModule(IServiceManager<ViewModelBase> vmFactory)
        : base(vmFactory)
    {
        Title = "Upscaler";
        AddCards(vmFactory.Get<UpscalerCardViewModel>());
    }

    /// <inheritdoc />
    protected override void OnApplyStep(ModuleApplyStepEventArgs e)
    {
        var card = GetCard<UpscalerCardViewModel>();

        // Skip if scale is close to 1
        if (Math.Abs(card.Scale - 1) < 0.005)
        {
            return;
        }

        var upscaleSize = e.Builder.Connections.PrimarySize.WithScale(card.Scale);

        var upscaleResult = e.Builder.Group_Upscale(
            e.Builder.Nodes.GetUniqueName("PostUpscale"),
            e.Builder.Connections.Primary ?? throw new ArgumentException("No Primary"),
            e.Builder.Connections.GetDefaultVAE(),
            card.SelectedUpscaler ?? throw new ValidationException("Upscaler is required"),
            upscaleSize.Width,
            upscaleSize.Height
        );

        e.Builder.Connections.Primary = upscaleResult;
        e.Builder.Connections.PrimarySize = upscaleSize;
    }
}
