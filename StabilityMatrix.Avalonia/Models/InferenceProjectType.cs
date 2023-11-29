using System;
using StabilityMatrix.Avalonia.ViewModels.Inference;

namespace StabilityMatrix.Avalonia.Models;

public enum InferenceProjectType
{
    Unknown,
    TextToImage,
    ImageToImage,
    Inpainting,
    Upscale,
    ImageToVideo
}

public static class InferenceProjectTypeExtensions
{
    public static Type? ToViewModelType(this InferenceProjectType type)
    {
        return type switch
        {
            InferenceProjectType.TextToImage => typeof(InferenceTextToImageViewModel),
            InferenceProjectType.ImageToImage => null,
            InferenceProjectType.Inpainting => null,
            InferenceProjectType.Upscale => typeof(InferenceImageUpscaleViewModel),
            InferenceProjectType.ImageToVideo => typeof(InferenceImageToVideoViewModel),
            InferenceProjectType.Unknown => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
