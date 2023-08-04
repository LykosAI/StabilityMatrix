using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(InferenceTextToImageModel))]
public class InferenceTextToImageModel
{
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }
    public string? SelectedModelName { get; set; }
    public SeedCardModel? SeedCardState { get; set; }
    public SamplerCardModel? SamplerCardState { get; set; }
}
