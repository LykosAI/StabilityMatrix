using System.Text.Json.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;
]
public class PromptCardModel
{
    public string? Prompt { get; init; }
    public string? NegativePrompt { get; init; }
    
    public JsonObject? ModulesCardState { get; init; }
}
