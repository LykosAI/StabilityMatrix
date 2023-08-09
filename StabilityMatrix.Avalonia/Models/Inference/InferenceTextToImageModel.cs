using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(InferenceTextToImageModel))]
public class InferenceTextToImageModel
{
    public string? SelectedModelName { get; init; }
    public JsonObject? SeedCardState { get; init; }
    public JsonObject? PromptCardState { get; init; }
    public JsonObject? StackCardState { get; init; }
}
