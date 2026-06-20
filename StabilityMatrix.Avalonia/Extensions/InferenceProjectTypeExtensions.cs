using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using StabilityMatrix.Avalonia.Languages;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Avalonia.ViewModels.Inference;
using StabilityMatrix.Core.Models.Inference;

namespace StabilityMatrix.Avalonia.Extensions;

public static class InferenceProjectTypeExtensions
{
    public static Type? ToViewModelType(this InferenceProjectType type)
    {
        return type switch
        {
            InferenceProjectType.TextToImage => typeof(InferenceTextToImageViewModel),
            InferenceProjectType.ImageToImage => typeof(InferenceImageToImageViewModel),
            InferenceProjectType.Inpainting => null,
            InferenceProjectType.Upscale => typeof(InferenceImageUpscaleViewModel),
            InferenceProjectType.ImageToVideo => typeof(InferenceImageToVideoViewModel),
            InferenceProjectType.FluxTextToImage => typeof(InferenceFluxTextToImageViewModel),
            InferenceProjectType.WanTextToVideo => typeof(InferenceWanTextToVideoViewModel),
            InferenceProjectType.WanImageToVideo => typeof(InferenceWanImageToVideoViewModel),
            InferenceProjectType.Unknown => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    /// <summary>
    /// All user-selectable project types with their picker display metadata, in display order.
    /// Types without a view model (Unknown, Inpainting) are intentionally excluded.
    /// </summary>
    public static IReadOnlyList<InferenceProjectTypeInfo> All { get; } =
        new InferenceProjectTypeInfo[]
        {
            new()
            {
                Type = InferenceProjectType.TextToImage,
                DisplayName = Resources.Label_TextToImage,
                Icon = Symbol.Image,
                Description =
                    "Generate images from a text prompt. Works with SD1.5, SDXL, Pony, Flux and more.",
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.ImageToImage,
                DisplayName = Resources.Label_ImageToImage,
                Icon = Symbol.ImageMultiple,
                Description = "Transform an existing image using a text prompt.",
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.Upscale,
                DisplayName = Resources.Label_Upscale,
                Icon = Symbol.ResizeImage,
                Description = "Increase the resolution of an image with an upscaling model.",
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.WanTextToVideo,
                DisplayName = "Wan Text to Video",
                Icon = Symbol.Video,
                Description = "Generate a video from a text prompt using Wan.",
                Category = InferenceProjectCategory.Video,
            },
            new()
            {
                Type = InferenceProjectType.WanImageToVideo,
                DisplayName = "Wan Image to Video",
                Icon = Symbol.VideoAdd,
                Description = "Animate an input image into a video using Wan.",
                Category = InferenceProjectCategory.Video,
            },
            new()
            {
                Type = InferenceProjectType.FluxTextToImage,
                DisplayName = "Flux Text to Image",
                Icon = Symbol.Sparkle,
                Description =
                    "Standalone Flux text-to-image. The Text to Image tab also handles "
                    + "Flux automatically.",
                Category = InferenceProjectCategory.Legacy,
            },
            new()
            {
                Type = InferenceProjectType.ImageToVideo,
                DisplayName = "SVD Image to Video",
                Icon = Symbol.VideoRecording,
                Description = "Stable Video Diffusion image-to-video.",
                Category = InferenceProjectCategory.Legacy,
            },
        };

    /// <summary>
    /// Returns the picker display metadata for a project type, or null if it has none
    /// (e.g. Unknown / not-yet-implemented types).
    /// </summary>
    public static InferenceProjectTypeInfo? GetInfo(this InferenceProjectType type) =>
        All.FirstOrDefault(x => x.Type == type);

    /// <summary>
    /// The project types grouped by category, in display order, for the grouped picker view.
    /// </summary>
    public static IReadOnlyList<InferenceProjectTypeCategoryGroup> GetGrouped() =>
        new[]
        {
            (InferenceProjectCategory.Image, Header: "Image"),
            (InferenceProjectCategory.Video, Header: "Video"),
            (InferenceProjectCategory.Legacy, Header: "Legacy"),
        }
            .Select(group => new InferenceProjectTypeCategoryGroup
            {
                Header = group.Header,
                Items = All.Where(x => x.Category == group.Item1).ToList(),
            })
            .Where(group => group.Items.Count > 0)
            .ToList();
}
