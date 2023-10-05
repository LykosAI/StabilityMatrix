namespace StabilityMatrix.Core.Inference;

public readonly record struct ComfyProgressUpdateEventArgs(
    int Value, 
    int Maximum, 
    string? TaskId,
    string? RunningNode);
