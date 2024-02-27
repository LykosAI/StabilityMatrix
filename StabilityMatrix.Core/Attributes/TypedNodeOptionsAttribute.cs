using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Core.Attributes;

/// <summary>
/// Options for <see cref="ComfyTypedNodeBase{TOutput}"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class TypedNodeOptionsAttribute : Attribute
{
    public string? Name { get; init; }

    public string[]? RequiredExtensions { get; init; }
}
