using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(InferenceTextToImageModel))]
public class InferenceTextToImageModel
{
    public string? Prompt { get; init; }
    public string? NegativePrompt { get; init; }
    public string? SelectedModelName { get; init; }
    public SeedCardModel? SeedCardState { get; init; }
    public SamplerCardModel? SamplerCardState { get; init; }
    public PromptCardModel? PromptCardState { get; init; }
}
