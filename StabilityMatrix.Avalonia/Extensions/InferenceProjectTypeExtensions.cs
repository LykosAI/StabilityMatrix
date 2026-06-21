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
                Description = Resources.Label_TextToImageDescription,
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.ImageToImage,
                DisplayName = Resources.Label_ImageToImage,
                Icon = Symbol.ImageMultiple,
                Description = Resources.Label_ImageToImageDescription,
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.Upscale,
                DisplayName = Resources.Label_Upscale,
                Icon = Symbol.ResizeImage,
                Description = Resources.Label_UpscaleDescription,
                Category = InferenceProjectCategory.Image,
            },
            new()
            {
                Type = InferenceProjectType.WanTextToVideo,
                DisplayName = Resources.Label_WanTextToVideo,
                Icon = Symbol.Video,
                Description = Resources.Label_WanTextToVideoDescription,
                Category = InferenceProjectCategory.Video,
            },
            new()
            {
                Type = InferenceProjectType.WanImageToVideo,
                DisplayName = Resources.Label_WanImageToVideo,
                Icon = Symbol.VideoAdd,
                Description = Resources.Label_WanImageToVideoDescription,
                Category = InferenceProjectCategory.Video,
            },
            new()
            {
                Type = InferenceProjectType.FluxTextToImage,
                DisplayName = Resources.Label_FluxTextToImage,
                Icon = Symbol.Sparkle,
                Description = Resources.Label_FluxTextToImageDescription,
                Category = InferenceProjectCategory.Legacy,
            },
            new()
            {
                Type = InferenceProjectType.ImageToVideo,
                DisplayName = Resources.Label_SvdImageToVideo,
                Icon = Symbol.VideoRecording,
                Description = Resources.Label_SvdImageToVideoDescription,
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
            (InferenceProjectCategory.Image, Header: Resources.Label_Image),
            (InferenceProjectCategory.Video, Header: Resources.Label_Video),
            (InferenceProjectCategory.Legacy, Header: Resources.Label_Legacy),
        }
            .Select(group => new InferenceProjectTypeCategoryGroup
            {
                Header = group.Header,
                Items = All.Where(x => x.Category == group.Item1).ToList(),
            })
            .Where(group => group.Items.Count > 0)
            .ToList();
}
