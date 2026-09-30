using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// LoRA file stems captured from a built generation graph for output naming.
/// </summary>
public sealed class GenerationLoraSnapshot
{
    /// <summary>
    /// A generation with no LoRA loaders.
    /// </summary>
    public static GenerationLoraSnapshot Empty { get; } = new([]);

    /// <summary>
    /// Sorted, distinct file stems, with path separators and filename characters removed or replaced.
    /// </summary>
    public ImmutableArray<string> Names { get; }

    /// <summary>
    /// The underscore-separated stems, or <c>no_lora</c> when no LoRA is present.
    /// </summary>
    public string FileNameValue => Names.IsEmpty ? "no_lora" : string.Join("_", Names);

    private GenerationLoraSnapshot(ImmutableArray<string> names)
    {
        Names = names;
    }

    /// <summary>
    /// Copies names from standard LoRA loaders, shared by prompt syntax and LoRA modules.
    /// Later edits to the graph do not change the snapshot.
    /// </summary>
    public static GenerationLoraSnapshot FromNodes(IEnumerable<ComfyNode> nodes)
    {
        var names = nodes
            .Where(node => node.ClassType == "LoraLoader")
            .Select(node => node.Inputs.GetValueOrDefault("lora_name") as string)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Path.GetFileNameWithoutExtension(name!.Replace('\\', '/').Split('/')[^1]))
            .Select(name => string.Concat(name.Select(c => c < ' ' || "<>:\"/\\|?*".Contains(c) ? '_' : c)))
            .Select(name => name.Trim(' ', '.'))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        return new GenerationLoraSnapshot(names);
    }
}
