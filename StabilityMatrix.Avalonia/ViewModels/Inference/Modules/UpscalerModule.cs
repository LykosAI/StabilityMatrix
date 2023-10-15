using System;
using AvaloniaEdit.Utils;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.Services;
using StabilityMatrix.Avalonia.ViewModels.Base;
using StabilityMatrix.Core.Extensions;

namespace StabilityMatrix.Avalonia.ViewModels.Inference.Modules;

public class UpscalerModule : ModuleBase
{
    /// <inheritdoc />
    public UpscalerModule(ServiceManager<ViewModelBase> vmFactory)
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

        var builder = e.Builder;
        var upscaleSize = builder.Connections.PrimarySize.WithScale(card.Scale);

        var upscaleResult = builder.Group_Upscale(
            "PostUpscale",
            builder.Connections.Primary!,
            builder.Connections.PrimaryVAE!,
            card.SelectedUpscaler!.Value,
            upscaleSize.Width,
            upscaleSize.Height
        );

        builder.Connections.Primary = upscaleResult;
        builder.Connections.PrimarySize = upscaleSize;
    }
}
