using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Models.Inference;

public class UpscalerCardModel
{
    public double Scale { get; init; } = 1;
    public ComfyUpscaler? SelectedUpscaler { get; init; }
}
