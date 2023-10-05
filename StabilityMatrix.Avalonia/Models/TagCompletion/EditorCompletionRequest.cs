using AvaloniaEdit.Document;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

public record EditorCompletionRequest : TextCompletionRequest
{
    public required ISegment Segment { get; init; }
}
