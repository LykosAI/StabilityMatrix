using System;

namespace StabilityMatrix.Avalonia.Models.TagCompletion;

/// <summary>
/// Type of completion requested.
/// </summary>
[Flags]
public enum CompletionType
{
    None = 0,
    Tag = 1 << 1,
    ExtraNetwork = 1 << 2,
    ExtraNetworkType = 1 << 3
}
