using System.Text.Json.Serialization;
using StabilityMatrix.Core.Models.Api.Comfy;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(UpscalerCardModel))]
public class UpscalerCardModel
{
    public double Scale { get; init; } = 1;
    public ComfyUpscaler? SelectedUpscaler { get; init; }
}
