using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Core.Models;

public class InferenceQueueCustomPromptEventArgs : EventArgs
{
    public ComfyNodeBuilder Builder { get; } = new();

    public NodeDictionary Nodes => Builder.Nodes;

    public long? SeedOverride { get; init; }

    public List<(string SourcePath, string DestinationRelativePath)> FilesToTransfer { get; init; } = [];
}
