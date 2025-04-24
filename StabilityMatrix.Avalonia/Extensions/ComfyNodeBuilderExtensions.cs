using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using StabilityMatrix.Avalonia.Models;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Extensions;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;
using StabilityMatrix.Core.Models.Api.Comfy.NodeTypes;

namespace StabilityMatrix.Avalonia.Extensions;

public static class ComfyNodeBuilderExtensions
{
    public static void SetupEmptyLatentSource(
        this ComfyNodeBuilder builder,
        int width,
        int height,
        int batchSize = 1,
        int? batchIndex = null,
        int? length = null,
        LatentType latentType = LatentType.Default
    )
    {
        var primaryNodeConnection = latentType switch
        {
            LatentType.Default
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.EmptyLatentImage
                        {
                            Name = "EmptyLatentImage",
                            BatchSize = batchSize,
                            Height = height,
                            Width = width
                        }
                    )
                    .Output,
            LatentType.Sd3
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.EmptySD3LatentImage
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.EmptySD3LatentImage)),
                            BatchSize = batchSize,
                            Height = height,
                            Width = width
                        }
                    )
                    .Output,
            LatentType.Hunyuan
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.EmptyHunyuanLatentVideo
                        {
                            Name = builder.Nodes.GetUniqueName(
                                nameof(ComfyNodeBuilder.EmptyHunyuanLatentVideo)
                            ),
                            BatchSize = batchSize,
                            Height = height,
                            Width = width,
                            Length =
                                length
                                ?? throw new ValidationException(
                                    "Length cannot be null when latentType is Hunyuan"
                                )
                        }
                    )
                    .Output,
            _ => throw new ArgumentOutOfRangeException(nameof(latentType), latentType, null)
        };

        builder.Connections.Primary = primaryNodeConnection;
        builder.Connections.PrimarySize = new Size(width, height);

        // If batch index is selected, add a LatentFromBatch
        if (batchIndex is not null)
        {
            builder.Connections.Primary = builder
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.LatentFromBatch
                    {
                        Name = "LatentFromBatch",
                        Samples = builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        BatchIndex = batchIndex.Value - 1,
                        Length = 1
                    }
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
        var loadImage = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage { Name = "LoadImage", Image = sourceImageRelativePath }
        );

        builder.Connections.Primary = loadImage.Output1;
        builder.Connections.PrimarySize = imageSize;

        // Add batch if selected
        if (builder.Connections.BatchSize > 1)
        {
            builder.Connections.Primary = builder
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.RepeatLatentBatch
                    {
                        Name = builder.Nodes.GetUniqueName("RepeatLatentBatch"),
                        Samples = builder.GetPrimaryAsLatent(),
                        Amount = builder.Connections.BatchSize
                    }
                )
                .Output;
        }

        // If batch index is selected, add a LatentFromBatch
        if (batchIndex is not null)
        {
            builder.Connections.Primary = builder
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.LatentFromBatch
                    {
                        Name = "LatentFromBatch",
                        Samples = builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        BatchIndex = batchIndex.Value - 1,
                        Length = 1
                    }
                )
                .Output;
        }
    }

    /// <summary>
    /// Setup an image as the <see cref="ComfyNodeBuilder.NodeBuilderConnections.Primary"/> connection
    /// </summary>
    public static void SetupImagePrimarySourceWithMask(
        this ComfyNodeBuilder builder,
        ImageSource image,
        Size imageSize,
        ImageSource mask,
        Size maskSize,
        int? batchIndex = null
    )
    {
        // Get image paths
        var sourceImageRelativePath = Path.Combine("Inference", image.GetHashGuidFileNameCached());
        var maskImageRelativePath = Path.Combine("Inference", mask.GetHashGuidFileNameCached());

        // Load image
        var loadImage = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImage
            {
                Name = builder.Nodes.GetUniqueName("LoadImage"),
                Image = sourceImageRelativePath
            }
        );

        // Load mask for alpha channel
        var loadMask = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.LoadImageMask
            {
                Name = builder.Nodes.GetUniqueName("LoadMask"),
                Image = maskImageRelativePath,
                Channel = "red"
            }
        );

        builder.Connections.Primary = loadImage.Output1;
        builder.Connections.PrimarySize = imageSize;

        // new betterer inpaint
        builder.Connections.Primary = builder
            .Nodes.AddTypedNode(
                new ComfyNodeBuilder.VAEEncode
                {
                    Name = builder.Nodes.GetUniqueName("VAEEncode"),
                    Pixels = loadImage.Output1,
                    Vae = builder.Connections.GetDefaultVAE()
                }
            )
            .Output;

        // latent noise mask for betterer inpaint
        builder.Connections.Primary = builder
            .Nodes.AddTypedNode(
                new ComfyNodeBuilder.SetLatentNoiseMask
                {
                    Name = builder.Nodes.GetUniqueName("SetLatentNoiseMask"),
                    Samples = builder.GetPrimaryAsLatent(),
                    Mask = loadMask.Output
                }
            )
            .Output;

        // Add batch if selected
        if (builder.Connections.BatchSize > 1)
        {
            builder.Connections.Primary = builder
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.RepeatLatentBatch
                    {
                        Name = builder.Nodes.GetUniqueName("RepeatLatentBatch"),
                        Samples = builder.GetPrimaryAsLatent(),
                        Amount = builder.Connections.BatchSize
                    }
                )
                .Output;
        }

        // If batch index is selected, add a LatentFromBatch
        if (batchIndex is not null)
        {
            builder.Connections.Primary = builder
                .Nodes.AddTypedNode(
                    new ComfyNodeBuilder.LatentFromBatch
                    {
                        Name = "LatentFromBatch",
                        Samples = builder.GetPrimaryAsLatent(),
                        // remote expects a 0-based index, vm is 1-based
                        BatchIndex = batchIndex.Value - 1,
                        Length = 1
                    }
                )
                .Output;
        }
    }

    public static string SetupOutputImage(this ComfyNodeBuilder builder)
    {
        if (builder.Connections.Primary is null)
            throw new ArgumentException("No Primary");

        var image = builder.Connections.Primary.Match(
            _ =>
                builder.GetPrimaryAsImage(
                    builder.Connections.PrimaryVAE
                        ?? builder.Connections.Refiner.VAE
                        ?? builder.Connections.Base.VAE
                        ?? throw new ArgumentException("No Primary, Refiner, or Base VAE")
                ),
            image => image
        );

        var previewImage = builder.Nodes.AddTypedNode(
            new ComfyNodeBuilder.PreviewImage
            {
                Name = builder.Nodes.GetUniqueName("SaveImage"),
                Images = image
            }
        );

        builder.Connections.OutputNodes.Add(previewImage);

        return previewImage.Name;
    }

    public static void SetupPlasmaLatentSource(
        this ComfyNodeBuilder builder,
        int width,
        int height,
        ulong seed,
        NoiseType noiseType,
        int valueMin = -1,
        int valueMax = -1,
        int redMin = -1,
        int redMax = -1,
        int greenMin = -1,
        int greenMax = -1,
        int blueMin = -1,
        int blueMax = -1,
        double turbulence = 2.75d
    )
    {
        var primaryNodeConnection = noiseType switch
        {
            NoiseType.Plasma
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.PlasmaNoise
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.PlasmaNoise)),
                            Height = height,
                            Width = width,
                            Seed = seed,
                            ValueMin = valueMin,
                            ValueMax = valueMax,
                            RedMin = redMin,
                            RedMax = redMax,
                            GreenMin = greenMin,
                            GreenMax = greenMax,
                            BlueMin = blueMin,
                            BlueMax = blueMax,
                            Turbulence = turbulence,
                        }
                    )
                    .Output,

            NoiseType.Random
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.RandNoise
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.RandNoise)),
                            Height = height,
                            Width = width,
                            Seed = seed,
                            ValueMin = valueMin,
                            ValueMax = valueMax,
                            RedMin = redMin,
                            RedMax = redMax,
                            GreenMin = greenMin,
                            GreenMax = greenMax,
                            BlueMin = blueMin,
                            BlueMax = blueMax
                        }
                    )
                    .Output,

            NoiseType.Greyscale
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.GreyNoise
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.GreyNoise)),
                            Height = height,
                            Width = width,
                            Seed = seed,
                            ValueMin = valueMin,
                            ValueMax = valueMax,
                            RedMin = redMin,
                            RedMax = redMax,
                            GreenMin = greenMin,
                            GreenMax = greenMax,
                            BlueMin = blueMin,
                            BlueMax = blueMax
                        }
                    )
                    .Output,

            NoiseType.Brown
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.BrownNoise
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.BrownNoise)),
                            Height = height,
                            Width = width,
                            Seed = seed,
                            ValueMin = valueMin,
                            ValueMax = valueMax,
                            RedMin = redMin,
                            RedMax = redMax,
                            GreenMin = greenMin,
                            GreenMax = greenMax,
                            BlueMin = blueMin,
                            BlueMax = blueMax
                        }
                    )
                    .Output,

            NoiseType.Pink
                => builder
                    .Nodes.AddTypedNode(
                        new ComfyNodeBuilder.PinkNoise
                        {
                            Name = builder.Nodes.GetUniqueName(nameof(ComfyNodeBuilder.PinkNoise)),
                            Height = height,
                            Width = width,
                            Seed = seed,
                            ValueMin = valueMin,
                            ValueMax = valueMax,
                            RedMin = redMin,
                            RedMax = redMax,
                            GreenMin = greenMin,
                            GreenMax = greenMax,
                            BlueMin = blueMin,
                            BlueMax = blueMax
                        }
                    )
                    .Output,
            _ => throw new ArgumentOutOfRangeException(nameof(noiseType), noiseType, null)
        };

        builder.Connections.Primary = primaryNodeConnection;
        builder.Connections.PrimarySize = new Size(width, height);
    }
}
