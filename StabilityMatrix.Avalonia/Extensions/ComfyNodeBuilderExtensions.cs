using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Avalonia.Extensions;

public static class ComfyNodeBuilderExtensions
{
    public static void SetupEmptyLatentSource(
        this ComfyNodeBuilder builder,
        int width,
        int height,
        int batchSize = 1,
        int? batchIndex = null
    )
    {
        var emptyLatent = builder
            .Nodes
            .AddTypedNode(
                new ComfyNodeBuilder.EmptyLatentImage
                {
                    Name = "EmptyLatentImage",
                    BatchSize = batchSize,
                    Height = height,
                    Width = width
                }
            );

        builder.Connections.Primary = emptyLatent.Output;
        builder.Connections.PrimarySize = new Size(width, height);

        // If batch index is selected, add a LatentFromBatch
        if (batchIndex is not null)
        {
            builder.Connections.Primary = builder
                .Nodes
                .AddNamedNode(
                    ComfyNodeBuilder.LatentFromBatch(
                        "LatentFromBatch",
                        builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        batchIndex.Value - 1,
                        1
                    )
                )
                .Output;
        }
    }

    /// <summary>
    /// Setup an image as the <see cref="ComfyNodeBuilder.NodeBuilderConnections.Primary"/> connection
    /// </summary>
    public static void SetupImagePrimarySource(
        this ComfyNodeBuilder builder,
        ImageSource image,
        Size imageSize,
        int? batchIndex = null
    )
    {
        // Get source image
        var sourceImageRelativePath = Path.Combine("Inference", image.GetHashGuidFileNameCached());

        // Load source
        var loadImage = builder
            .Nodes
            .AddTypedNode(new ComfyNodeBuilder.LoadImage { Name = "LoadImage", Image = sourceImageRelativePath });

        builder.Connections.Primary = loadImage.Output1;
        builder.Connections.PrimarySize = imageSize;

        // If batch index is selected, add a LatentFromBatch
        if (batchIndex is not null)
        {
            builder.Connections.Primary = builder
                .Nodes
                .AddNamedNode(
                    ComfyNodeBuilder.LatentFromBatch(
                        "LatentFromBatch",
                        builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        batchIndex.Value - 1,
                        1
                    )
                )
                .Output;
        }
    }

    public static string SetupOutputImage(this ComfyNodeBuilder builder)
    {
        if (builder.Connections.Primary is null)
            throw new ArgumentException("No Primary");

        var image = builder
            .Connections
            .Primary
            .Match(
                _ =>
                    builder.GetPrimaryAsImage(
                        builder.Connections.PrimaryVAE
                            ?? builder.Connections.Refiner.VAE
                            ?? builder.Connections.Base.VAE
                            ?? throw new ArgumentException("No Primary, Refiner, or Base VAE")
                    ),
                image => image
            );

        var previewImage = builder
            .Nodes
            .AddTypedNode(
                new ComfyNodeBuilder.PreviewImage { Name = builder.Nodes.GetUniqueName("SaveImage"), Images = image }
            );

        builder.Connections.OutputNodes.Add(previewImage);

        return previewImage.Name;
    }
}
