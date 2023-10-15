using System;
using System.Collections.Generic;
using StabilityMatrix.Core.Models.Api.Comfy.Nodes;

namespace StabilityMatrix.Avalonia.Models.Inference;

public class ModuleApplyStepEventArgs : EventArgs
{
    public required ComfyNodeBuilder Builder { get; init; }

    /// <summary>
    /// Index of the step in the pipeline.
    /// </summary>
    public int StepIndex { get; init; }

    /// <summary>
    /// Index
    /// </summary>
    public int StepTypeIndex { get; init; }

    public IReadOnlyDictionary<Type, bool> IsEnabledOverrides { get; init; } =
        new Dictionary<Type, bool>();
}
