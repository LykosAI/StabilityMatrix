using System;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// Event for view models requesting to load their view state from a provided state
/// </summary>
public class LoadViewStateEventArgs : EventArgs
{
    public required ViewState State { get; init; }
}
