using System.Text.Json.Serialization;

namespace StabilityMatrix.Avalonia.Models.Inference;

[JsonSerializable(typeof(PromptCardModel))]
public class PromptCardModel
{
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }
}
