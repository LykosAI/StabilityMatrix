using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;

public class InferenceTextToImageModel
{
    public string? SelectedModelName { get; init; }
    public JsonObject? SeedCardState { get; init; }
    public JsonObject? PromptCardState { get; init; }
    public JsonObject? StackCardState { get; init; }
}
