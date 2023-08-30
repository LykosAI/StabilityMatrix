using System.Collections.Generic;
using AvaloniaEdit.Document;
using StabilityMatrix.Avalonia.Models.Inference.Tokens;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public record TextCompletionRequest
{
    public required CompletionType Type { get; init; }
    public required string Text { get; init; }
    public PromptExtraNetworkType ExtraNetworkTypes { get; init; } = new();
}
